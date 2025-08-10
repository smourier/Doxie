using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;

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
/// One, or several overlapping tokens, along with the score(s) and the scope of
/// the original text
/// </summary>
public class TokenGroup(TokenStream tokenStream)
{
    public const int MaxNumTokensPerGroup = 50;

    private readonly Token[] _tokens = new Token[MaxNumTokensPerGroup];
    private readonly float[] _scores = new float[MaxNumTokensPerGroup];
    private readonly IOffsetAttribute _offsetAtt = tokenStream.AddAttribute<IOffsetAttribute>(); // LUCENENET: marked readonly
    private readonly ICharTermAttribute _termAtt = tokenStream.AddAttribute<ICharTermAttribute>(); // LUCENENET: marked readonly

    internal int MatchStartOffset { get; set; }
    internal int MatchEndOffset { get; set; }

    /// <summary>
    /// the number of tokens in this group
    /// </summary>
    public virtual int NumTokens { get; internal set; } = 0;

    /// <summary>
    /// the start position in the original text
    /// </summary>
    public virtual int StartOffset { get; internal set; } = 0;

    /// <summary>
    /// the end position in the original text
    /// </summary>
    public virtual int EndOffset { get; private set; } = 0;

    /// <summary>
    /// all tokens' scores summed up
    /// </summary>
    public virtual float TotalScore { get; private set; }

    internal void AddToken(float score)
    {
        if (NumTokens < MaxNumTokensPerGroup)
        {
            int termStartOffset = _offsetAtt.StartOffset;
            int termEndOffset = _offsetAtt.EndOffset;
            if (NumTokens == 0)
            {
                StartOffset = MatchStartOffset = termStartOffset;
                EndOffset = MatchEndOffset = termEndOffset;
                TotalScore += score;
            }
            else
            {
                StartOffset = Math.Min(StartOffset, termStartOffset);
                EndOffset = Math.Max(EndOffset, termEndOffset);
                if (score > 0)
                {
                    if (TotalScore == 0)
                    {
                        MatchStartOffset = termStartOffset;
                        MatchEndOffset = termEndOffset;
                    }
                    else
                    {
                        MatchStartOffset = Math.Min(MatchStartOffset, termStartOffset);
                        MatchEndOffset = Math.Max(MatchEndOffset, termEndOffset);
                    }
                    TotalScore += score;
                }
            }

            var token = new Token(termStartOffset, termEndOffset);
            token.SetEmpty().Append(_termAtt);
            _tokens[NumTokens] = token;
            _scores[NumTokens] = score;
            NumTokens++;
        }
    }

    internal bool IsDistinct() => _offsetAtt.StartOffset >= EndOffset;

    internal void Clear()
    {
        NumTokens = 0;
        TotalScore = 0;
    }

    /// <summary>
    /// the "n"th token
    /// </summary>
    /// <param name="index">a value between 0 and numTokens -1</param>
    public virtual Token GetToken(int index) => _tokens[index];

    /// <summary>
    /// the "n"th score
    /// </summary>
    /// <param name="index">a value between 0 and numTokens -1</param>
    public virtual float GetScore(int index) => _scores[index];
}
