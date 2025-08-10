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
/// <see cref="IFragmenter"/> implementation which breaks text up into same-size
/// fragments with no concerns over spotting sentence boundaries.
/// </summary>
/// <param name="fragmentSize">size in number of characters of each fragment</param>
public class SimpleFragmenter(int fragmentSize) : IFragmenter
{
    public const int DefaultFragmentSize = 100;
    private int _currentNumFrags;
    private IOffsetAttribute? _offsetAtt;

    public SimpleFragmenter() : this(DefaultFragmentSize) { }

    /// <summary>
    /// <seealso cref="IFragmenter.Start(string, TokenStream)"/>
    /// </summary>
    public virtual void Start(string originalText, TokenStream stream)
    {
        _offsetAtt = stream.AddAttribute<IOffsetAttribute>();
        _currentNumFrags = 1;
    }

    /// <summary>
    /// <seealso cref="IFragmenter.IsNewFragment()"/>
    /// </summary>
    public virtual bool IsNewFragment()
    {
        if (_offsetAtt == null)
            throw new InvalidOperationException("Start must be called before IsNewFragment");

        var isNewFrag = _offsetAtt.EndOffset >= FragmentSize * _currentNumFrags;
        if (isNewFrag)
        {
            _currentNumFrags++;
        }
        return isNewFrag;
    }

    /// <summary>
    /// Gets or Sets size in number of characters of each fragment
    /// </summary>
    public virtual int FragmentSize { get; set; } = fragmentSize;
}
