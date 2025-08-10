using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Search;
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
/// Class used to markup highlighted terms found in the best sections of a
/// text, using configurable <see cref="IFragmenter"/>, <see cref="Scorer"/>, <see cref="IFormatter"/>,
/// <see cref="IEncoder"/> and tokenizers.
/// </summary>
public class Highlighter(IScorer fragmentScorer)
{
    public static readonly int DefaultMaxCharsToAnalyze = 50 * 1024;

    public virtual int MaxDocCharsToAnalyze { get; set; } = DefaultMaxCharsToAnalyze;
    public virtual IFragmenter TextFragmenter { get; set; } = new SimpleFragmenter();
    public virtual IScorer FragmentScorer { get; set; } = fragmentScorer;

    /// <summary>
    /// Highlights chosen terms in a text, extracting the most relevant section.
    /// This is a convenience method that calls <see cref="GetBestFragment(TokenStream, string)"/>
    /// </summary>
    /// <param name="analyzer">the analyzer that will be used to split <paramref name="text"/> into chunks</param>
    /// <param name="fieldName">Name of field used to influence analyzer's tokenization policy</param>
    /// <param name="text">text to highlight terms in</param>
    /// <returns>highlighted text fragment or null if no terms found</returns>
    /// <exception cref="InvalidTokenOffsetsException">thrown if any token's EndOffset exceeds the provided text's length</exception>
    public string? GetBestFragment(Analyzer analyzer, string fieldName, string text)
    {
        var tokenStream = analyzer.GetTokenStream(fieldName, text);
        return GetBestFragment(tokenStream, text);
    }

    /// <summary>
    /// Highlights chosen terms in a text, extracting the most relevant section.
    /// The document text is analysed in chunks to record hit statistics
    /// across the document. After accumulating stats, the fragment with the highest score
    /// is returned
    /// </summary>
    /// <param name="tokenStream">
    /// A stream of tokens identified in the text parameter, including offset information.
    /// This is typically produced by an analyzer re-parsing a document's
    /// text. Some work may be done on retrieving TokenStreams more efficiently
    /// by adding support for storing original text position data in the Lucene
    /// index but this support is not currently available (as of Lucene 1.4 rc2).
    /// </param>
    /// <param name="text">text to highlight terms in</param>
    /// <returns>highlighted text fragment or null if no terms found</returns>
    /// <exception cref="InvalidTokenOffsetsException">thrown if any token's EndOffset exceeds the provided text's length</exception>
    public string? GetBestFragment(TokenStream tokenStream, string text)
    {
        var results = GetBestFragments(tokenStream, text, 1);
        if (results.Length > 0)
            return results[0];

        return null;
    }

    /// <summary>
    /// Highlights chosen terms in a text, extracting the most relevant sections.
    /// This is a convenience method that calls <see cref="GetBestFragments(TokenStream, string, int)"/>
    /// </summary>
    /// <param name="analyzer">the analyzer that will be used to split <paramref name="text"/> into chunks</param>
    /// <param name="fieldName">the name of the field being highlighted (used by analyzer)</param>
    /// <param name="text">text to highlight terms in</param>
    /// <param name="maxNumFragments">the maximum number of fragments.</param>
    /// <returns>highlighted text fragments (between 0 and <paramref name="maxNumFragments"/> number of fragments)</returns>
    /// <exception cref="InvalidTokenOffsetsException">thrown if any token's EndOffset exceeds the provided text's length</exception>
    public string[] GetBestFragments(
        Analyzer analyzer,
        string fieldName,
        string text,
        int maxNumFragments)
    {
        var tokenStream = analyzer.GetTokenStream(fieldName, text);
        return GetBestFragments(tokenStream, text, maxNumFragments);
    }

    /// <summary>
    /// Highlights chosen terms in a text, extracting the most relevant sections.
    /// The document text is analysed in chunks to record hit statistics
    /// across the document. After accumulating stats, the fragments with the highest scores
    /// are returned as an array of strings in order of score (contiguous fragments are merged into
    /// one in their original order to improve readability)
    /// </summary>
    /// <param name="tokenStream"></param>
    /// <param name="text">text to highlight terms in</param>
    /// <param name="maxNumFragments">the maximum number of fragments.</param>
    /// <returns>highlighted text fragments (between 0 and <paramref name="maxNumFragments"/> number of fragments)</returns>
    /// <exception cref="InvalidTokenOffsetsException">thrown if any token's EndOffset exceeds the provided text's length</exception>
    public string[] GetBestFragments(TokenStream tokenStream, string text, int maxNumFragments)
    {
        maxNumFragments = Math.Max(1, maxNumFragments); //sanity check

        var frag = GetBestTextFragments(tokenStream, text, true, maxNumFragments);

        //Get text
        var fragTexts = new JCG.List<string>();
        for (var i = 0; i < frag.Length; i++)
        {
            if (frag[i] != null && frag[i].Score > 0)
            {
                fragTexts.Add(frag[i].ToString());
            }
        }
        return [.. fragTexts];
    }

    /// <summary>
    /// Low level api to get the most relevant (formatted) sections of the document.
    /// This method has been made public to allow visibility of score information held in <see cref="TextFragment"/> objects.
    /// Thanks to Jason Calabrese for help in redefining the interface.
    /// </summary>
    /// <exception cref="IOException">If there is a low-level I/O error</exception>
    /// <exception cref="InvalidTokenOffsetsException">thrown if any token's EndOffset exceeds the provided text's length</exception>
    public TextFragment[] GetBestTextFragments(
        TokenStream tokenStream,
        string text,
        bool mergeContiguousFragments,
        int maxNumFragments)
    {
        var docFrags = new JCG.List<TextFragment>();
        var newText = new StringBuilder();

        var termAtt = tokenStream.AddAttribute<ICharTermAttribute>();
        var offsetAtt = tokenStream.AddAttribute<IOffsetAttribute>();
        tokenStream.Reset();
        var currentFrag = new TextFragment(newText, newText.Length, docFrags.Count);

        if (FragmentScorer is QueryScorer queryScorer)
        {
            queryScorer.SetMaxDocCharsToAnalyze(MaxDocCharsToAnalyze);
        }

        var newStream = FragmentScorer.Init(tokenStream);
        if (newStream != null)
        {
            tokenStream = newStream;
        }
        FragmentScorer.StartFragment(currentFrag);
        docFrags.Add(currentFrag);

        var fragQueue = new FragmentQueue(maxNumFragments);

        try
        {
            string tokenText;
            int startOffset;
            int endOffset;
            int lastEndOffset = 0;
            TextFragmenter.Start(text, tokenStream);

            var tokenGroup = new TokenGroup(tokenStream);

            for (var next = tokenStream.IncrementToken();
                 next && offsetAtt.StartOffset < MaxDocCharsToAnalyze;
                 next = tokenStream.IncrementToken())
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
                        //record stats for a new fragment
                        currentFrag.TextEndPos = newText.Length;
                        currentFrag = new TextFragment(newText, newText.Length, docFrags.Count);
                        FragmentScorer.StartFragment(currentFrag);
                        docFrags.Add(currentFrag);
                    }
                }

                tokenGroup.AddToken(FragmentScorer.GetTokenScore());
            }
            currentFrag.Score = FragmentScorer.FragmentScore;

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

            //sort the most relevant sections of the text
            foreach (var f in docFrags)
            {
                currentFrag = f;

                //If you are running with a version of Lucene before 11th Sept 03
                // you do not have PriorityQueue.insert() - so uncomment the code below
                /*
                                    if (currentFrag.getScore() >= minScore)
                                    {
                                        fragQueue.put(currentFrag);
                                        if (fragQueue.size() > maxNumFragments)
                                        { // if hit queue overfull
                                            fragQueue.pop(); // remove lowest in hit queue
                                            minScore = ((TextFragment) fragQueue.top()).getScore(); // reset minScore
                                        }
                                    }
                */
                //The above code caused a problem as a result of Christoph Goller's 11th Sept 03
                //fix to PriorityQueue. The correct method to use here is the new "insert" method
                // USE ABOVE CODE IF THIS DOES NOT COMPILE!
                fragQueue.InsertWithOverflow(currentFrag);
            }

            //return the most relevant fragments
            var frag = new TextFragment[fragQueue.Count];
            for (var i = frag.Length - 1; i >= 0; i--)
            {
                frag[i] = fragQueue.Pop();
            }

            //merge any contiguous fragments to improve readability
            if (mergeContiguousFragments)
            {
                MergeContiguousFragments(frag);
                var fragTexts = new JCG.List<TextFragment>();
                for (var i = 0; i < frag.Length; i++)
                {
                    if (frag[i] != null && frag[i].Score > 0)
                    {
                        fragTexts.Add(frag[i]);
                    }
                }
                frag = new TextFragment[fragTexts.Count];
                fragTexts.CopyTo(frag);
            }

            return frag;

        }
        finally
        {
            if (tokenStream != null)
            {
                try
                {
                    tokenStream.End();
                    tokenStream.Dispose();
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>
    /// Improves readability of a score-sorted list of TextFragments by merging any fragments
    /// that were contiguous in the original text into one larger fragment with the correct order.
    /// This will leave a "null" in the array entry for the lesser scored fragment. 
    /// </summary>
    /// <param name="frag">An array of document fragments in descending score</param>
    private static void MergeContiguousFragments(TextFragment?[] frag) // LUCENENET: CA1822: Mark members as static
    {
        bool mergingStillBeingDone;
        if (frag.Length > 1)
            do
            {
                mergingStillBeingDone = false; //initialise loop control flag
                //for each fragment, scan other frags looking for contiguous blocks
                for (var i = 0; i < frag.Length; i++)
                {
                    if (frag[i] is null)
                        continue;

                    //merge any contiguous blocks 
                    for (var x = 0; x < frag.Length; x++)
                    {
                        var fragX = frag[x];
                        if (fragX is null)
                            continue;

                        var fragI = frag[i];
                        if (fragI is null)
                            break;

                        TextFragment? frag1 = null;
                        TextFragment? frag2 = null;
                        var frag1Num = 0;
                        var frag2Num = 0;
                        int bestScoringFragNum;
                        int worstScoringFragNum;
                        //if blocks are contiguous....
                        if (fragI.Follows(fragX))
                        {
                            frag1 = frag[x];
                            frag1Num = x;
                            frag2 = frag[i];
                            frag2Num = i;
                        }
                        else if (fragX.Follows(fragI))
                        {
                            frag1 = frag[i];
                            frag1Num = i;
                            frag2 = frag[x];
                            frag2Num = x;
                        }
                        //merging required..
                        if (frag1 != null && frag2 != null)
                        {
                            if (frag1.Score > frag2.Score)
                            {
                                bestScoringFragNum = frag1Num;
                                worstScoringFragNum = frag2Num;
                            }
                            else
                            {
                                bestScoringFragNum = frag2Num;
                                worstScoringFragNum = frag1Num;
                            }
                            frag1.Merge(frag2);
                            frag[worstScoringFragNum] = null;
                            mergingStillBeingDone = true;
                            frag[bestScoringFragNum] = frag1;
                        }
                    }
                }
            } while (mergingStillBeingDone);
    }

    /// <summary>
    /// Highlights terms in the <paramref name="text"/>, extracting the most relevant sections
    /// and concatenating the chosen fragments with a separator (typically "...").
    /// The document text is analysed in chunks to record hit statistics
    /// across the document. After accumulating stats, the fragments with the highest scores
    /// are returned in order as "separator" delimited strings.
    /// </summary>
    /// <param name="tokenStream"></param>
    /// <param name="text">text to highlight terms in</param>
    /// <param name="maxNumFragments">the maximum number of fragments.</param>
    /// <param name="separator">the separator used to intersperse the document fragments (typically "...")</param>
    /// <returns>highlighted text</returns>
    /// <exception cref="InvalidTokenOffsetsException">thrown if any token's EndOffset exceeds the provided text's length</exception>
    public virtual string GetBestFragments(
        TokenStream tokenStream,
        string text,
        int maxNumFragments,
        string separator)
    {
        var sections = GetBestFragments(tokenStream, text, maxNumFragments);
        var result = new StringBuilder();
        for (var i = 0; i < sections.Length; i++)
        {
            if (i > 0)
            {
                result.Append(separator);
            }
            result.Append(sections[i]);
        }
        return result.ToString();
    }

    private sealed class FragmentQueue(int size) : PriorityQueue<TextFragment>(size)
    {
        protected override bool LessThan(TextFragment fragA, TextFragment fragB)
        {
            if (fragA.Score == fragB.Score)
                return fragA.FragNum > fragB.FragNum;
            else
                return fragA.Score < fragB.Score;
        }
    }
}
