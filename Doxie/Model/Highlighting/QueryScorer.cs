using J2N.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
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

///<summary>
/// <see cref="IScorer"/> implementation which scores text fragments by the number of
/// unique query terms found. This class converts appropriate <see cref="Query"/>s to
/// <see cref="Search.Spans.SpanQuery"/>s and attempts to score only those terms that participated in
/// generating the 'hit' on the document.
/// </summary>
public class QueryScorer : IScorer
{
    private float _totalScore;
    private JCG.HashSet<string>? _foundTerms;
    private IDictionary<string, WeightedSpanTerm>? _fieldWeightedSpanTerms;
    private readonly float _maxTermWeight;
    private int _position = -1;
    private readonly string? _defaultField;
    private ICharTermAttribute? _termAtt;
    private IPositionIncrementAttribute? _posIncAtt;
    private Query? _query;
    private string? _field;
    private IndexReader? _reader;
    private readonly bool _skipInitExtractor;
    private bool _wrapToCaching = true;
    private int _maxCharsToAnalyze;

    /// <summary>
    /// Constructs a new <see cref="QueryScorer"/> instance
    /// </summary>
    /// <param name="query"><see cref="Query"/> to use for highlighting</param>
    public QueryScorer(Query query)
    {
        Init(query, null, null, true);
    }

    /// <summary>
    /// Constructs a new <see cref="QueryScorer"/> instance
    /// </summary>
    /// <param name="query"><see cref="Query"/> to use for highlighting</param>
    /// <param name="field">Field to highlight - pass null to ignore fields</param>
    public QueryScorer(Query query, string field)
    {
        Init(query, field, null, true);
    }

    /// <summary>
    /// Constructs a new <see cref="QueryScorer"/> instance
    /// </summary>
    /// <param name="query"><see cref="Query"/> to use for highlighting</param>
    /// <param name="reader"><see cref="IndexReader"/> to use for quasi tf/idf scoring</param>
    /// <param name="field">Field to highlight - pass null to ignore fields</param>
    public QueryScorer(Query query, IndexReader reader, string field)
    {
        Init(query, field, reader, true);
    }

    /// <summary>
    /// Constructs a new <see cref="QueryScorer"/> instance
    /// </summary>
    /// <param name="query"><see cref="Query"/> to use for highlighting</param>
    /// <param name="reader"><see cref="IndexReader"/> to use for quasi tf/idf scoring</param>
    /// <param name="field">Field to highlight - pass null to ignore fields</param>
    /// <param name="defaultField">The default field for queries with the field name unspecified</param>
    public QueryScorer(Query query, IndexReader reader, string field, string defaultField)
    {
        ArgumentNullException.ThrowIfNull(defaultField);
        _defaultField = defaultField.Intern();
        Init(query, field, reader, true);
    }

    /// <summary>
    /// Constructs a new <see cref="QueryScorer"/> instance
    /// </summary>
    /// <param name="query"><see cref="Query"/> to use for highlighting</param>
    /// <param name="field">Field to highlight - pass null to ignore fields</param>
    /// <param name="defaultField">The default field for queries with the field name unspecified</param>
    public QueryScorer(Query query, string field, string defaultField)
    {
        ArgumentNullException.ThrowIfNull(defaultField);
        _defaultField = defaultField.Intern();
        Init(query, field, null, true);
    }

    /// <summary>
    /// Constructs a new <see cref="QueryScorer"/> instance
    /// </summary>
    /// <param name="weightedTerms">an array of pre-created <see cref="WeightedSpanTerm"/>s</param>
    public QueryScorer(WeightedSpanTerm[] weightedTerms)
    {
        ArgumentNullException.ThrowIfNull(weightedTerms);
        _fieldWeightedSpanTerms = new JCG.Dictionary<string, WeightedSpanTerm>(weightedTerms.Length);

        foreach (var t in weightedTerms)
        {
            if (!_fieldWeightedSpanTerms.TryGetValue(t.Term, out var existingTerm) ||
                existingTerm is null ||
                existingTerm.Weight < t.Weight)
            {
                // if a term is defined more than once, always use the highest
                // scoring Weight
                _fieldWeightedSpanTerms[t.Term] = t;
                _maxTermWeight = Math.Max(_maxTermWeight, t.Weight);
            }
        }
        _skipInitExtractor = true;
    }

    /// <seealso cref="IScorer.FragmentScore"/>
    public virtual float FragmentScore => _totalScore;

    /// <summary>
    /// The highest weighted term (useful for passing to <see cref="GradientFormatter"/> to set top end of coloring scale).
    /// </summary>
    public virtual float MaxTermWeight => _maxTermWeight;

    /// <summary>
    /// Controls whether or not multi-term queries are expanded
    /// against a <see cref="Index.Memory.MemoryIndex"/> <see cref="IndexReader"/>.
    /// <c>true</c> if multi-term queries should be expanded
    /// </summary>
    public virtual bool ExpandMultiTermQuery { get; set; } = true;

    /// <seealso cref="IScorer.GetTokenScore()"/>
    public virtual float GetTokenScore()
    {
        if (_fieldWeightedSpanTerms is null || _termAtt is null || _posIncAtt is null || _foundTerms == null)
            throw new InvalidOperationException("QueryScorer must be initialized with a Query before calling GetTokenScore");

        _position += _posIncAtt.PositionIncrement;
        var termText = _termAtt.ToString();

        if (!_fieldWeightedSpanTerms.TryGetValue(termText, out var weightedSpanTerm) || weightedSpanTerm is null)
            return 0;

        if (weightedSpanTerm.IsPositionSensitive && !weightedSpanTerm.CheckPosition(_position))
            return 0;

        var score = weightedSpanTerm.Weight;

        // found a query term - is it unique in this doc?
        if (!_foundTerms.Contains(termText))
        {
            _totalScore += score;
            _foundTerms.Add(termText);
        }

        return score;
    }

    /// <seealso cref="IScorer.Init"/>
    public virtual TokenStream? Init(TokenStream tokenStream)
    {
        _position = -1;
        _termAtt = tokenStream.AddAttribute<ICharTermAttribute>();
        _posIncAtt = tokenStream.AddAttribute<IPositionIncrementAttribute>();
        if (!_skipInitExtractor)
        {
            _fieldWeightedSpanTerms?.Clear();
            return InitExtractor(tokenStream);
        }
        return null;
    }

    /// <summary>
    /// Retrieve the <see cref="WeightedSpanTerm"/> for the specified token. Useful for passing
    /// Span information to a <see cref="IFragmenter"/>.
    /// </summary>
    /// <param name="token">token to get <see cref="WeightedSpanTerm"/> for</param>
    /// <returns><see cref="WeightedSpanTerm"/> for token</returns>
    public virtual WeightedSpanTerm? GetWeightedSpanTerm(string token)
    {
        if (_fieldWeightedSpanTerms == null)
            return null;

        _fieldWeightedSpanTerms.TryGetValue(token, out var result);
        return result;
    }

    private void Init(Query query, string? field, IndexReader? reader, bool expandMultiTermQuery)
    {
        ArgumentNullException.ThrowIfNull(query);
        _reader = reader;
        ExpandMultiTermQuery = expandMultiTermQuery;
        _query = query;
        _field = field;
    }

    private TokenStream? InitExtractor(TokenStream tokenStream)
    {
        if (_query == null)
            throw new InvalidOperationException("QueryScorer must be initialized with a Query before calling InitExtractor");

        var qse = NewTermExtractor(_defaultField);
        qse.SetMaxDocCharsToAnalyze(_maxCharsToAnalyze);
        qse.ExpandMultiTermQuery = ExpandMultiTermQuery;
        qse.SetWrapIfNotCachingTokenFilter(_wrapToCaching);
        if (_reader is null)
        {
            _fieldWeightedSpanTerms = qse.GetWeightedSpanTerms(_query, tokenStream, _field);
        }
        else
        {
            _fieldWeightedSpanTerms = qse.GetWeightedSpanTermsWithScores(_query, tokenStream, _field, _reader);
        }
        if (qse.IsCachedTokenStream)
        {
            return qse.TokenStream;
        }

        return null;
    }

    protected virtual WeightedSpanTermExtractor NewTermExtractor(string? defaultField) => defaultField is null ? new WeightedSpanTermExtractor() : new WeightedSpanTermExtractor(defaultField);

    /// <seealso cref="IScorer.StartFragment"/>
    public virtual void StartFragment(TextFragment newFragment)
    {
        _foundTerms = [];
        _totalScore = 0;
    }

    /// <summary>
    /// By default, <see cref="TokenStream"/>s that are not of the type
    /// <see cref="CachingTokenFilter"/> are wrapped in a <see cref="CachingTokenFilter"/> to
    /// ensure an efficient reset - if you are already using a different caching
    /// <see cref="TokenStream"/> impl and you don't want it to be wrapped, set this to
    /// false.
    /// </summary>
    public virtual void SetWrapIfNotCachingTokenFilter(bool wrap) => _wrapToCaching = wrap;

    public virtual void SetMaxDocCharsToAnalyze(int maxDocCharsToAnalyze) => _maxCharsToAnalyze = maxDocCharsToAnalyze;
}