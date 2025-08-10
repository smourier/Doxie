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
/// Low-level class used to record information about a section of a document 
/// with a score.
/// </summary>
public class TextFragment(StringBuilder markedUpText, int textStartPos, int fragNum)
{
    private readonly StringBuilder _markedUpText = markedUpText ?? throw new ArgumentNullException(nameof(markedUpText));

    public float Score { get; internal set; }
    public int TextEndPos { get; internal set; }
    public int TextStartPos { get; internal set; } = textStartPos;

    /// <summary>
    /// the fragment sequence number
    /// </summary>
    public int FragNum { get; } = fragNum;

    /// <summary>
    /// Returns the marked-up text for this text fragment 
    /// </summary>
    public override string ToString() => _markedUpText.ToString(TextStartPos, TextEndPos - TextStartPos);
}