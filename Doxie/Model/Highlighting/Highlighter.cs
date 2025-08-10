using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Search;

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
/// Class used to markup highlighted terms found in the best sections of a
/// text, using configurable <see cref="IFragmenter"/>, <see cref="Scorer"/>, <see cref="IFormatter"/>,
/// <see cref="IEncoder"/> and tokenizers.
/// </summary>
public class Highlighter(IScorer fragmentScorer)
{
    public static readonly int DefaultMaxCharsToAnalyze = int.MaxValue;// 50 * 1024;

    public virtual int MaxDocCharsToAnalyze { get; set; } = DefaultMaxCharsToAnalyze;
    public virtual IFragmenter TextFragmenter { get; set; } = new SimpleFragmenter();
    public virtual IScorer FragmentScorer { get; set; } = fragmentScorer;

    /// <summary>
    /// Low level api to get the most relevant (formatted) sections of the document.
    /// This method has been made public to allow visibility of score information held in <see cref="TextFragment"/> objects.
    /// Thanks to Jason Calabrese for help in redefining the interface.
    /// 
    /// Simon Mourier modified it with the following changes:
    /// * only return the text fragments that have a score > 0
    /// * don't use encoder nor formatter, just return the raw text fragments
    /// * build stream token ourselves
    /// * default max chars to analyze is now int.MaxValue, not 50 * 1024
    /// * removed the max number of fragments to return, now return all fragments
    /// * removed the priority queue thing and merge feature which posed perf problems to achieve previous point
    /// * removed other methods of this class that where all based on this one and can be rewritten easily using linq
    /// note: ideally, this method should return a real IEnumerable of TextFragment instead of a list
    /// </summary>
    public IReadOnlyList<TextFragment> GetBestTextFragments(Analyzer analyzer, string field, string text)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(text);

        var tokenStream = analyzer.GetTokenStream(field, text);
        var currentStream = tokenStream;
        var docFrags = new List<TextFragment>();
        var newText = new StringBuilder();
        var termAtt = currentStream.AddAttribute<ICharTermAttribute>();
        var offsetAtt = currentStream.AddAttribute<IOffsetAttribute>();
        currentStream.Reset();
        var currentFrag = new TextFragment(newText, newText.Length, docFrags.Count);

        if (FragmentScorer is QueryScorer queryScorer)
        {
            queryScorer.SetMaxDocCharsToAnalyze(MaxDocCharsToAnalyze);
        }

        var newStream = FragmentScorer.Init(currentStream);
        if (newStream != null)
        {
            currentStream.Dispose();
            currentStream = newStream;
        }

        try
        {
            FragmentScorer.StartFragment(currentFrag);

            string tokenText;
            int startOffset;
            int endOffset;
            int lastEndOffset = 0;
            TextFragmenter.Start(text, currentStream);

            var tokenGroup = new TokenGroup(currentStream);

            for (var next = currentStream.IncrementToken(); next && offsetAtt.StartOffset < MaxDocCharsToAnalyze; next = currentStream.IncrementToken())
            {
                if (offsetAtt.EndOffset > text.Length || offsetAtt.StartOffset > text.Length)
                    throw new Exception("Token " + termAtt.ToString() + " exceeds length of provided text sized " + text.Length);

                if (tokenGroup.NumTokens > 0 && tokenGroup.IsDistinct())
                {
                    //the current token is distinct from previous tokens -
                    // markup the cached token group info
                    startOffset = tokenGroup.MatchStartOffset;
                    endOffset = tokenGroup.MatchEndOffset;
                    tokenText = text[startOffset..endOffset];
                    //store any whitespace etc from between this and last group
                    if (startOffset > lastEndOffset)
                    {
                        newText.Append(text[lastEndOffset..startOffset]);
                    }

                    newText.Append(tokenText);
                    lastEndOffset = Math.Max(endOffset, lastEndOffset);
                    tokenGroup.Clear();

                    //check if current token marks the start of a new fragment
                    if (TextFragmenter.IsNewFragment())
                    {
                        currentFrag.Score = FragmentScorer.FragmentScore;
                        if (currentFrag.Score > 0)
                        {
                            docFrags.Add(currentFrag);
                        }

                        //record stats for a new fragment
                        currentFrag.TextEndPos = newText.Length;
                        currentFrag = new TextFragment(newText, newText.Length, docFrags.Count);
                        FragmentScorer.StartFragment(currentFrag);
                    }
                }

                tokenGroup.AddToken(FragmentScorer.GetTokenScore());
            }

            currentFrag.Score = FragmentScorer.FragmentScore;
            if (currentFrag.Score > 0)
            {
                docFrags.Add(currentFrag);
            }

            if (tokenGroup.NumTokens > 0)
            {
                //flush the accumulated text (same code as in above loop)
                startOffset = tokenGroup.MatchStartOffset;
                endOffset = tokenGroup.MatchEndOffset;
                tokenText = text[startOffset..endOffset];
                //store any whitespace etc from between this and last group
                if (startOffset > lastEndOffset)
                {
                    newText.Append(text[lastEndOffset..startOffset]);
                }

                newText.Append(tokenText);
                lastEndOffset = Math.Max(lastEndOffset, endOffset);
            }

            //Test what remains of the original text beyond the point where we stopped analyzing 
            if (lastEndOffset < text.Length && text.Length <= MaxDocCharsToAnalyze)
            {
                //append it to the last fragment
                newText.Append(text[lastEndOffset..]);
            }

            currentFrag.TextEndPos = newText.Length;
            return docFrags;
        }
        finally
        {
            currentStream.Dispose();
        }
    }
}
