﻿// Author: Daniele Giardini - http://www.demigiant.com
// Created: 2014/07/10 14:33
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 

using DG.Tween.Core;
using DG.Tween.Plugins.Core;
using UnityEngine;

namespace DG.Tween.Plugins.DefaultPlugins
{
    public class ColorPlugin : ABSTweenPlugin<Color, Color, NoOptions>
    {
        public override Color ConvertT1toT2(NoOptions options, Color value)
        {
            return value;
        }

        public override Color GetRelativeEndValue(NoOptions options, Color startValue, Color changeValue)
        {
            return startValue + changeValue;
        }

        public override Color GetChangeValue(NoOptions options, Color startValue, Color endValue)
        {
            return endValue - startValue;
        }

        public override Color Evaluate(NoOptions options, bool isRelative, MemberGetter<Color> getter, float elapsed, Color startValue, Color changeValue, float duration, EaseFunction ease)
        {
            startValue.r = ease(elapsed, startValue.r, changeValue.r, duration, 0, 0);
            startValue.g = ease(elapsed, startValue.g, changeValue.g, duration, 0, 0);
            startValue.b = ease(elapsed, startValue.b, changeValue.b, duration, 0, 0);
            startValue.a = ease(elapsed, startValue.a, changeValue.a, duration, 0, 0);
            return startValue;
        }
    }
}