﻿// Author: Daniele Giardini - http://www.demigiant.com
// Created: 2014/05/07 12:56
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

using System;
using DG.Tween.Core;
using DG.Tween.Core.Easing;
using DG.Tween.Plugins.Core;
using UnityEngine;

namespace DG.Tween
{
    public sealed class Tweener<T> : Tween
    {
        // OPTIONS ///////////////////////////////////////////////////

        internal float delay;
        internal bool isRelative;
        internal EaseFunction ease;
        internal EaseCurve easeCurve; // Stored in case of AnimationCurve ease

        // SETUP DATA ////////////////////////////////////////////////

        new internal readonly Type type = typeof(T);
        MemberGetter<T> _getter;
        MemberSetter<T> _setter;
        T _startValue, _endValue;
        ABSTweenPlugin<T> _tweenPlugin;

        // PLAY DATA /////////////////////////////////////////////////

        const float _Epsilon = 0.0000001f;
        internal bool delayComplete; // TRUE when the delay has elapsed (also set by Delay extension method)
        float _elapsedDelay; // Amount of eventual delay elapsed

        // ***********************************************************************************
        // CONSTRUCTOR
        // ***********************************************************************************

        internal Tweener()
        {
            tweenType = TweenType.Tweener;
            Reset();
        }

        // ===================================================================================
        // PUBLIC METHODS --------------------------------------------------------------------

        public override void Reset()
        {
            base.Reset();
            DoReset(this);
        }

        // Also called by TweenManager at each update.
        // Returns TRUE if the tween needs to be killed
        public override bool Goto(float to)
        {
            return DoGoto(this, to);
        }

        // ===================================================================================
        // INTERNAL METHODS ------------------------------------------------------------------

        // Called by DOTween when spawning/creating a new Tweener
        internal static void Setup(Tweener<T> t, MemberGetter<T> getter, MemberSetter<T> setter, T endValue, float duration)
        {
            t._getter = getter;
            t._setter = setter;
            t._endValue = endValue;
            t.duration = duration;
            if (t._tweenPlugin == null) t._tweenPlugin = PluginsManager.GetPlugin<T>();
        }

        // ===================================================================================
        // METHODS ---------------------------------------------------------------------------

        // _tweenPlugin is not reset since it's useful to keep it as a reference
        static void DoReset(Tweener<T> t)
        {
            t.delay = 0;
            t.isRelative = false;
            t.ease = Quad.EaseOut;
            t.easeCurve = null;

            t._getter = null;
            t._setter = null;

            t.delayComplete = true;
            t._elapsedDelay = 0;
        }

        // Called the moment the tween starts, AFTER any delay has elapsed
        static void Startup(Tweener<T> t)
        {
            t.startupDone = true;
            t.fullDuration = t.loops > -1 ? t.duration * t.loops : Mathf.Infinity;
            t._startValue = t._getter();
            if (t.isRelative) t._endValue = t._tweenPlugin.GetRelativeEndValue(t._startValue, t._endValue);
        }

        // Instead of advancing the tween from the previous position each time,
        // uses the given position to calculate running time since startup, and places the tween there like a Goto.
        // Executes regardless of whether the tween is playing,
        // but not if the tween result would be a completion or rewind, and the tween is already there
        static bool DoGoto(Tweener<T> t, float to)
        {
            // TODO Prevent any action if we determine that the tween should end as rewinded/complete and it's already in such a state
            // FIXME behaves like if timeScale was ridiculously low (and also stutters) after thousands of completed loops due to float not allowing enough decimals

            // Lock creation extensions
            t.creationLocked = true;

            float prevElapsed = t.elapsed;
            t.elapsed = to;
            if (t.elapsed < 0) t.elapsed = 0;
            bool wasComplete = t.isComplete;
            bool wasDelayComplete = t.delayComplete; // Stored for an eventual onDelayComplete callback
            int newCompletedSteps = 0;

            // Delay
            if (t.delay > 0) {
                t._elapsedDelay = t.elapsed;
                if (t._elapsedDelay >= t.delay) {
                    t.delayComplete = true;
                    t.elapsed = t._elapsedDelay - t.delay;
                    t._elapsedDelay = t.delay;
                } else t.elapsed = 0;
            }

            // Update
            if (t.elapsed > 0 || prevElapsed > 0) {
                // Startup
                if (!t.startupDone) Startup(t);
                // Elapsed
                if (t.elapsed > t.fullDuration) t.elapsed = t.fullDuration;
                // Check if it will be complete
                t.isComplete = t.elapsed >= t.fullDuration;
                // Loops - takes care of floating points imprecision, to avoid things like "2/2 = 0.99999" from happening
                int prevCompletedLoops = t.completedLoops;
                if (t.duration <= 0) t.completedLoops = 1;
                else {
                    float div = t.elapsed / t.duration;
                    int ceil = (int)Math.Ceiling(div);
                    if (ceil - div < _Epsilon) t.completedLoops = ceil;
                    else t.completedLoops = ceil - 1;
                }
                if (t.completedLoops > prevCompletedLoops) newCompletedSteps = t.completedLoops - prevCompletedLoops;
                // Position
                t.position = (t.elapsed > t.duration) ? t.elapsed % t.duration : t.elapsed;
                if (t.position <= 0 && t.elapsed > 0) t.position = t.duration; // Makes position 0 equal to position "end" when looping
                // Get values from plugin and set them
                float easePosition = t.position; // Changes in case we're yoyoing backwards
                if (t.loopType == LoopType.Yoyo && (!t.isComplete ? t.completedLoops % 2 != 0 : t.completedLoops % 2 == 0)) {
                    // Behaves differently in case the tween is complete or not,
                    // in order to make position 0 equal to position "end"
                    easePosition = t.duration - t.position;
                }
                T newVal = t._tweenPlugin.GetValue(easePosition, t._startValue, t._endValue, t.duration, t.ease);
                if (DOTween.useSafeMode) {
                    try {
                        t._setter(newVal);
                    } catch (MissingReferenceException) {
                        // Target/field doesn't exist anymore: kill tween
                        return true;
                    }
                } else {
                    t._setter(newVal);
                }
                // Set playing state
                if (!t.isBackwards && t.isComplete && t.isPlaying) t.isPlaying = false;
                else if (t.isBackwards && t.elapsed <= 0 && t.isPlaying) t.isPlaying = false;
            }

            // Callbacks
            if (newCompletedSteps > 0) {
                if (t.onStepComplete != null) {
                    for (int i = 0; i < newCompletedSteps; ++i) t.onStepComplete();
                }
            }
            if (t.isComplete && !wasComplete) {
                if (t.onComplete != null) t.onComplete();
            }

            // Return
            return t.autoKill && t.isComplete;
        }
    }
}