using J2N.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Index.Memory;
using Lucene.Net.Queries;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Lucene.Net.Util;
using JCG = J2N.Collections.Generic;

namespace Doxie.Model.Highlighting;

/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/// <summary>
/// Class used to extract <see cref="WeightedSpanTerm"/>s from a <see cref="Query"/> based on whether 
/// <see cref="Term"/>s from the <see cref="Query"/> are contained in a supplied <see cref="Analysis.TokenStream"/>.
/// </summary>
public class WeightedSpanTermExtractor
{
    private string? _fieldName;
    private TokenStream? _tokenStream;
    private readonly string? _defaultField;
    private bool _cachedTokenStream;
    private bool _wrapToCaching = true;
    private int _maxDocCharsToAnalyze;
    private AtomicReader? _internalReader;

    public WeightedSpanTermExtractor(string? defaultField = null)
    {
        if (defaultField != null)
        {
            _defaultField = defaultField.Intern();
        }
    }

    public virtual bool ExpandMultiTermQuery { get; set; }
    public virtual bool IsCachedTokenStream => _cachedTokenStream;
    public virtual TokenStream? TokenStream => _tokenStream;

    /// <summary>
    /// Fills a <see cref="T:IDictionary{string,WeightedSpanTerm}"/> with <see cref="WeightedSpanTerm"/>s using the terms from the supplied <paramref name="query"/>.
    /// </summary>
    /// <param name="query"><see cref="Query"/> to extract Terms from</param>
    /// <param name="terms">Map to place created <see cref="WeightedSpanTerm"/>s in</param>
    /// <exception cref="IOException">If there is a low-level I/O error</exception>
    protected virtual void Extract(Query query, IDictionary<string, WeightedSpanTerm> terms)
    {
        if (query is BooleanQuery booleanQuery)
        {
            var queryClauses = booleanQuery.Clauses;
            for (var i = 0; i < queryClauses.Count; i++)
            {
                if (!queryClauses[i].IsProhibited)
                {
                    Extract(queryClauses[i].Query, terms);
                }
            }
        }
        else if (query is PhraseQuery phraseQuery)
        {
            var phraseQueryTerms = phraseQuery.GetTerms();
            var clauses = new SpanQuery[phraseQueryTerms.Length];
            for (var i = 0; i < phraseQueryTerms.Length; i++)
            {
                clauses[i] = new SpanTermQuery(phraseQueryTerms[i]);
            }

            var slop = phraseQuery.Slop;
            var positions = phraseQuery.GetPositions();
            // add largest position increment to slop
            if (positions.Length > 0)
            {
                var lastPos = positions[0];
                var largestInc = 0;
                var sz = positions.Length;
                for (var i = 1; i < sz; i++)
                {
                    var pos = positions[i];
                    var inc = pos - lastPos;
                    if (inc > largestInc)
                    {
                        largestInc = inc;
                    }

                    lastPos = pos;
                }

                if (largestInc > 1)
                {
                    slop += largestInc;
                }
            }

            var inorder = slop == 0;
            var sp = new SpanNearQuery(clauses, slop, inorder) { Boost = query.Boost };
            ExtractWeightedSpanTerms(terms, sp);
        }
        else if (query is TermQuery)
        {
            ExtractWeightedTerms(terms, query);
        }
        else if (query is SpanQuery spanQuery)
        {
            ExtractWeightedSpanTerms(terms, spanQuery);
        }
        else if (query is FilteredQuery filteredQuery)
        {
            Extract(filteredQuery.Query, terms);
        }
        else if (query is ConstantScoreQuery constantScoreQuery)
        {
            var q = constantScoreQuery.Query;
            if (q != null)
            {
                Extract(q, terms);
            }
        }
        else if (query is CommonTermsQuery)
        {
            // specialized since rewriting would change the result query 
            // this query is TermContext sensitive.
            ExtractWeightedTerms(terms, query);
        }
        else if (query is DisjunctionMaxQuery disjunctionMaxQuery)
        {
            foreach (var q in disjunctionMaxQuery)
            {
                Extract(q, terms);
            }
        }
        else if (query is MultiPhraseQuery mpq)
        {
            var termArrays = mpq.GetTermArrays();
            var positions = mpq.GetPositions();
            if (positions.Length > 0)
            {
                var maxPosition = positions[^1];
                for (var i = 0; i < positions.Length - 1; ++i)
                {
                    if (positions[i] > maxPosition)
                    {
                        maxPosition = positions[i];
                    }
                }

                var disjunctLists = new JCG.List<SpanQuery>[maxPosition + 1];
                var distinctPositions = 0;

                for (var i = 0; i < termArrays.Count; ++i)
                {
                    var termArray = termArrays[i];
                    var disjuncts = disjunctLists[positions[i]];
                    if (disjuncts is null)
                    {
                        disjuncts = disjunctLists[positions[i]] = new JCG.List<SpanQuery>(termArray.Length);
                        ++distinctPositions;
                    }
                    foreach (var term in termArray)
                    {
                        disjuncts.Add(new SpanTermQuery(term));
                    }
                }

                var positionGaps = 0;
                var position = 0;
                var clauses = new SpanQuery[distinctPositions];
                foreach (var disjuncts in disjunctLists)
                {
                    if (disjuncts != null)
                    {
                        clauses[position++] = new SpanOrQuery([.. disjuncts]);
                    }
                    else
                    {
                        ++positionGaps;
                    }
                }

                var slop = mpq.Slop;
                var inorder = slop == 0;

                var sp = new SpanNearQuery(clauses, slop + positionGaps, inorder)
                {
                    Boost = query.Boost
                };
                ExtractWeightedSpanTerms(terms, sp);
            }
        }
        else
        {
            var origQuery = query;
            if (query is MultiTermQuery)
            {
                if (!ExpandMultiTermQuery)
                    return;

                var copy = (MultiTermQuery)query.Clone();
                copy.MultiTermRewriteMethod = MultiTermQuery.SCORING_BOOLEAN_QUERY_REWRITE;
                origQuery = copy;
            }

            var reader = GetLeafContext().Reader;
            var rewritten = origQuery.Rewrite(reader);
            if (rewritten != origQuery)
            {
                // only rewrite once and then flatten again - the rewritten query could have a speacial treatment
                // if this method is overwritten in a subclass or above in the next recursion
                Extract(rewritten, terms);
            }
        }
        ExtractUnknownQuery(query, terms);
    }

    protected virtual void ExtractUnknownQuery(Query query, IDictionary<string, WeightedSpanTerm> terms)
    {
        // for sub-classing to extract custom queries
    }

    /// <summary>
    /// Fills a <see cref="T:IDictionary{string,WeightedSpanTerm}"/> with <see cref="WeightedSpanTerm"/>s using the terms from the supplied <see cref="SpanQuery"/>.
    /// </summary>
    /// <param name="terms"><see cref="T:IDictionary{string,WeightedSpanTerm}"/> to place created <see cref="WeightedSpanTerm"/>s in</param>
    /// <param name="spanQuery"><see cref="SpanQuery"/> to extract Terms from</param>
    /// <exception cref="IOException">If there is a low-level I/O error</exception>
    protected virtual void ExtractWeightedSpanTerms(IDictionary<string, WeightedSpanTerm> terms, SpanQuery spanQuery)
    {
        JCG.HashSet<string> fieldNames;

        if (_fieldName is null)
        {
            fieldNames = [];
            CollectSpanQueryFields(spanQuery, fieldNames);
        }
        else
        {
            fieldNames = [_fieldName];
        }

        // To support the use of the default field name
        if (_defaultField != null)
        {
            fieldNames.Add(_defaultField);
        }

        var queries = new JCG.Dictionary<string, SpanQuery>();
        var nonWeightedTerms = new JCG.HashSet<Term>();
        var mustRewriteQuery = MustRewriteQuery(spanQuery);
        if (mustRewriteQuery)
        {
            foreach (var field in fieldNames)
            {
                var rewrittenQuery = (SpanQuery)spanQuery.Rewrite(GetLeafContext().Reader);
                queries[field] = rewrittenQuery;
                rewrittenQuery.ExtractTerms(nonWeightedTerms);
            }
        }
        else
        {
            spanQuery.ExtractTerms(nonWeightedTerms);
        }

        var spanPositions = new JCG.List<PositionSpan>();

        foreach (var field in fieldNames)
        {
            var q = mustRewriteQuery ? queries[field] : spanQuery;
            if (q is null)
                continue;

            var context = GetLeafContext();
            var termContexts = new JCG.Dictionary<Term, TermContext>();
            var extractedTerms = new JCG.SortedSet<Term>();
            q.ExtractTerms(extractedTerms);
            foreach (var term in extractedTerms)
            {
                termContexts[term] = TermContext.Build(context, term);
            }

            var acceptDocs = context.AtomicReader.LiveDocs;
            var spans = q.GetSpans(context, acceptDocs, termContexts);

            // collect span positions
            while (spans.MoveNext())
            {
                spanPositions.Add(new PositionSpan(spans.Start, spans.End - 1));
            }
        }

        if (spanPositions.Count == 0)
        {
            // no spans found
            return;
        }

        foreach (var queryTerm in nonWeightedTerms)
        {
            if (FieldNameComparer(queryTerm.Field))
            {
                if (!terms.TryGetValue(queryTerm.Text, out var weightedSpanTerm) || weightedSpanTerm is null)
                {
                    weightedSpanTerm = new WeightedSpanTerm(spanQuery.Boost, queryTerm.Text);
                    weightedSpanTerm.AddPositionSpans(spanPositions);
                    weightedSpanTerm.IsPositionSensitive = true;
                    terms[queryTerm.Text] = weightedSpanTerm;
                }
                else
                {
                    if (spanPositions.Count > 0)
                    {
                        weightedSpanTerm.AddPositionSpans(spanPositions);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Fills a <see cref="T:IDictionary{string,WeightedSpanTerm}"/> with <see cref="WeightedSpanTerm"/>s using the terms from 
    /// the supplied <see cref="Search.Spans.SpanQuery"/>.
    /// </summary>
    /// <param name="terms"><see cref="T:IDictionary{string,WeightedSpanTerm}"/> to place created <see cref="WeightedSpanTerm"/>s in</param>
    /// <param name="query"><see cref="Query"/> to extract Terms from</param>
    /// <exception cref="IOException">If there is a low-level I/O error</exception>
    protected virtual void ExtractWeightedTerms(IDictionary<string, WeightedSpanTerm> terms, Query query)
    {
        var nonWeightedTerms = new JCG.HashSet<Term>();
        query.ExtractTerms(nonWeightedTerms);

        foreach (var queryTerm in nonWeightedTerms)
        {
            if (FieldNameComparer(queryTerm.Field))
            {
                var weightedSpanTerm = new WeightedSpanTerm(query.Boost, queryTerm.Text);
                terms[queryTerm.Text] = weightedSpanTerm;
            }
        }
    }

    /// <summary>
    /// Necessary to implement matches for queries against <see cref="_defaultField"/>
    /// </summary>
    protected virtual bool FieldNameComparer(string fieldNameToCheck) =>
        _fieldName is null ||
        _fieldName.Equals(fieldNameToCheck, StringComparison.Ordinal) ||
        fieldNameToCheck.Equals(_defaultField, StringComparison.Ordinal);

    protected virtual AtomicReaderContext GetLeafContext()
    {
        if (_tokenStream is null)
            throw new InvalidOperationException("TokenStream must be set before calling GetLeafContext");

        if (_internalReader is null)
        {
            if (_wrapToCaching && _tokenStream is not CachingTokenFilter)
            {
                _tokenStream = new CachingTokenFilter(new OffsetLimitTokenFilter(_tokenStream, _maxDocCharsToAnalyze));
                _cachedTokenStream = true;
            }

            var indexer = new MemoryIndex(true);
            indexer.AddField(DelegatingAtomicReader.FieldName, _tokenStream);
            _tokenStream.Reset();
            var searcher = indexer.CreateSearcher();

            // MEM index has only atomic ctx
            var reader = ((AtomicReaderContext)searcher.TopReaderContext).AtomicReader;
            _internalReader = new DelegatingAtomicReader(reader);
        }
        return _internalReader.AtomicContext;
    }

    /// <summary>
    /// This reader will just delegate every call to a single field in the wrapped
    /// <see cref="AtomicReader"/>. This way we only need to build this field once rather than
    /// N-Times
    /// </summary>
    internal sealed class DelegatingAtomicReader : FilterAtomicReader
    {
        public static string FieldName = "shadowed_field";

        internal DelegatingAtomicReader(AtomicReader reader) : base(reader) { }

        public override FieldInfos FieldInfos => throw new NotSupportedException();
        public override Fields Fields => new DelegatingFilterFields(base.Fields);

        private class DelegatingFilterFields(Fields fields) : FilterFields(fields)
        {
            public override Terms GetTerms(string field) => base.GetTerms(FieldName);

            public override IEnumerator<string> GetEnumerator()
            {
                var list = new JCG.List<string> { FieldName };
                return list.GetEnumerator();
            }

            public override int Count => 1;
        }

        public override NumericDocValues GetNumericDocValues(string field) => base.GetNumericDocValues(FieldName);
        public override BinaryDocValues GetBinaryDocValues(string field) => base.GetBinaryDocValues(FieldName);
        public override SortedDocValues GetSortedDocValues(string field) => base.GetSortedDocValues(FieldName);
        public override NumericDocValues GetNormValues(string field) => base.GetNormValues(FieldName);
        public override IBits GetDocsWithField(string field) => base.GetDocsWithField(FieldName);
    }

    /// <summary>
    /// Creates an <see cref="T:IDictionary{string,WeightedSpanTerm}"/> from the given <see cref="Query"/> and <see cref="Analysis.TokenStream"/>.
    /// </summary>
    /// <param name="query"><see cref="Query"/> that caused hit</param>
    /// <param name="tokenStream"><see cref="Analysis.TokenStream"/> of text to be highlighted</param>
    /// <param name="fieldName">restricts Term's used based on field name</param>
    /// <returns>Map containing <see cref="WeightedSpanTerm"/>s</returns>
    /// <exception cref="IOException">If there is a low-level I/O error</exception>
    public virtual IDictionary<string, WeightedSpanTerm> GetWeightedSpanTerms(Query query, TokenStream tokenStream, string? fieldName = null)
    {
        if (fieldName != null)
        {
            _fieldName = fieldName.Intern();
        }
        else
        {
            _fieldName = null;
        }

        var terms = new PositionCheckingMap<string>();
        _tokenStream = tokenStream;
        try
        {
            Extract(query, terms);
        }
        finally
        {
            IOUtils.Dispose(_internalReader);
        }

        return terms;
    }

    /// <summary>
    /// Creates an <see cref="T:IDictionary{string,WeightedSpanTerm}"/> from the given <see cref="Query"/> and <see cref="Analysis.TokenStream"/>. Uses a supplied
    /// <see cref="IndexReader"/> to properly Weight terms (for gradient highlighting).
    /// </summary>
    /// <param name="query"><see cref="Query"/> that caused hit</param>
    /// <param name="tokenStream"><see cref="Analysis.TokenStream"/> of text to be highlighted</param>
    /// <param name="fieldName">restricts Term's used based on field name</param>
    /// <param name="reader">to use for scoring</param>
    /// <returns>Map of <see cref="WeightedSpanTerm"/>s with quasi tf/idf scores</returns>
    /// <exception cref="IOException">If there is a low-level I/O error</exception>
    public virtual IDictionary<string, WeightedSpanTerm> GetWeightedSpanTermsWithScores(Query query, TokenStream tokenStream, string? fieldName, IndexReader reader)
    {
        _fieldName = fieldName?.Intern();

        _tokenStream = tokenStream;

        var terms = new PositionCheckingMap<string>();
        Extract(query, terms);

        var totalNumDocs = reader.MaxDoc;
        var weightedTerms = terms.Keys;

        try
        {
            foreach (var wt in weightedTerms)
            {
                terms.TryGetValue(wt, out WeightedSpanTerm weightedSpanTerm);
                var docFreq = reader.DocFreq(new Term(fieldName, weightedSpanTerm.Term));

                // IDF algorithm taken from DefaultSimilarity class
                var idf = (float)(Math.Log(totalNumDocs / (double)(docFreq + 1)) + 1.0);
                weightedSpanTerm.Weight *= idf;
            }
        }
        finally
        {
            IOUtils.Dispose(_internalReader);
        }

        return terms;
    }

    protected virtual void CollectSpanQueryFields(SpanQuery spanQuery, ISet<string> fieldNames)
    {
        if (spanQuery is FieldMaskingSpanQuery fieldMaskingSpanQuery)
        {
            CollectSpanQueryFields(fieldMaskingSpanQuery.MaskedQuery, fieldNames);
        }
        else if (spanQuery is SpanFirstQuery spanFirstQuery)
        {
            CollectSpanQueryFields(spanFirstQuery.Match, fieldNames);
        }
        else if (spanQuery is SpanNearQuery spanNearQuery)
        {
            foreach (var clause in spanNearQuery.GetClauses())
            {
                CollectSpanQueryFields(clause, fieldNames);
            }
        }
        else if (spanQuery is SpanNotQuery spanNotQuery)
        {
            CollectSpanQueryFields(spanNotQuery.Include, fieldNames);
        }
        else if (spanQuery is SpanOrQuery spanOrQuery)
        {
            foreach (var clause in spanOrQuery.GetClauses())
            {
                CollectSpanQueryFields(clause, fieldNames);
            }
        }
        else
        {
            fieldNames.Add(spanQuery.Field);
        }
    }

    protected virtual bool MustRewriteQuery(SpanQuery spanQuery)
    {
        if (!ExpandMultiTermQuery)
            return false; // Will throw NotImplementedException in case of a SpanRegexQuery.

        if (spanQuery is FieldMaskingSpanQuery fieldMaskingSpanQuery)
            return MustRewriteQuery(fieldMaskingSpanQuery.MaskedQuery);

        if (spanQuery is SpanFirstQuery spanFirstQuery)
            return MustRewriteQuery(spanFirstQuery.Match);

        if (spanQuery is SpanNearQuery spanNearQuery)
        {
            foreach (var clause in spanNearQuery.GetClauses())
            {
                if (MustRewriteQuery(clause))
                    return true;
            }
            return false;
        }

        if (spanQuery is SpanNotQuery spanNotQuery)
            return MustRewriteQuery(spanNotQuery.Include) || MustRewriteQuery(spanNotQuery.Exclude);

        if (spanQuery is SpanOrQuery spanOrQuery)
        {
            foreach (var clause in spanOrQuery.GetClauses())
            {
                if (MustRewriteQuery(clause))
                    return true;
            }
            return false;
        }

        if (spanQuery is SpanTermQuery)
            return false;

        return true;
    }


    /// <summary>
    /// This class makes sure that if both position sensitive and insensitive
    /// versions of the same term are added, the position insensitive one wins.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    // LUCENENET NOTE: Unfortunately, members of Dictionary{TKey, TValue} are not virtual,
    // so we need to implement IDictionary{TKey, TValue} instead.
    protected class PositionCheckingMap<K> : IDictionary<K, WeightedSpanTerm> where K : notnull
    {
        private readonly IDictionary<K, WeightedSpanTerm> _wrapped = new Dictionary<K, WeightedSpanTerm>();

        public WeightedSpanTerm this[K key]
        {
            get => _wrapped[key];

            set
            {
                _wrapped.TryGetValue(key, out var prev);
                _wrapped[key] = value;

                if (prev is null)
                    return;

                var prevTerm = prev;
                var newTerm = value;
                if (!prevTerm.IsPositionSensitive)
                {
                    newTerm.IsPositionSensitive = false;
                }
            }
        }

        public bool TryGetValue(K key, out WeightedSpanTerm value)
        {
            if (!_wrapped.TryGetValue(key, out var v))
            {
                value = new WeightedSpanTerm(0, string.Empty);
                return false;
            }

            value = v;
            return true;
        }

        public int Count => _wrapped.Count;
        public bool IsReadOnly => false;
        public ICollection<K> Keys => _wrapped.Keys;
        public ICollection<WeightedSpanTerm> Values => _wrapped.Values;
        public void Add(KeyValuePair<K, WeightedSpanTerm> item) => this[item.Key] = item.Value;
        public void Add(K key, WeightedSpanTerm value) => this[key] = value;
        public void Clear() => _wrapped.Clear();
        public bool Contains(KeyValuePair<K, WeightedSpanTerm> item) => _wrapped.Contains(item);
        public bool ContainsKey(K key) => _wrapped.ContainsKey(key);
        public void CopyTo(KeyValuePair<K, WeightedSpanTerm>[] array, int arrayIndex) => _wrapped.CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<K, WeightedSpanTerm>> GetEnumerator() => _wrapped.GetEnumerator();
        public bool Remove(KeyValuePair<K, WeightedSpanTerm> item) => _wrapped.Remove(item);
        public bool Remove(K key) => _wrapped.Remove(key);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// By default, <see cref="Analysis.TokenStream"/>s that are not of the type
    /// <see cref="CachingTokenFilter"/> are wrapped in a <see cref="CachingTokenFilter"/> to
    /// <see cref="Analysis.TokenStream"/> impl and you don't want it to be wrapped, set this to
    /// false.
    /// </summary>
    public virtual void SetWrapIfNotCachingTokenFilter(bool wrap) => _wrapToCaching = wrap;

    protected internal void SetMaxDocCharsToAnalyze(int maxDocCharsToAnalyze) => _maxDocCharsToAnalyze = maxDocCharsToAnalyze;
}