﻿// Author: Daniele Giardini - http://www.demigiant.com
// Created: 2014/05/07 13:00
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
using System.Collections.Generic;
using DG.Tween.Core.Enums;
using UnityEngine;

namespace DG.Tween.Core
{
    internal static class TweenManager
    {
        const int _DefaultMaxTweeners = 200;
        const int _DefaultMaxSequences = 50;
        const string _MaxTweenersReached = "Max number of Tweeners has been reached, capacity is now being automatically increased. Use DOTween.SetTweensCapacity to set it manually at startup";

        internal static int maxTweeners = _DefaultMaxTweeners;
        internal static int maxSequences = _DefaultMaxSequences;
        internal static bool hasActiveTweens, hasActiveDefaultTweens, hasActiveFixedTweens, hasActiveIndependentTweens;
        internal static int totActiveDefaultTweens, totActiveFixedTweens, totActiveIndependentTweens;
        internal static int totPooledTweeners, totPooledSequences;
        internal static int totTweeners, totSequences; // Both active and pooled

        static readonly List<Tween> _ActiveDefaultTweens = new List<Tween>(_DefaultMaxTweeners + _DefaultMaxSequences);
        static readonly List<Tween> _ActiveFixedTweens = new List<Tween>(_DefaultMaxTweeners + _DefaultMaxSequences);
        static readonly List<Tween> _ActiveIndependentTweens = new List<Tween>(_DefaultMaxTweeners + _DefaultMaxSequences);
        static readonly List<Tween> _PooledTweeners = new List<Tween>(_DefaultMaxTweeners);
        static readonly List<Tween> _PooledSequences = new List<Tween>(_DefaultMaxSequences);

        static readonly List<Tween> _KillList = new List<Tween>(_DefaultMaxTweeners);
        static readonly List<int> _KillIds = new List<int>(_DefaultMaxTweeners);

        // ===================================================================================
        // PUBLIC METHODS --------------------------------------------------------------------

        // Returns a new Tweener, from the pool if there's one available,
        // otherwise by instantiating a new one
        internal static Tweener<T1,T2> GetTweener<T1,T2>(UpdateType updateType, bool playNow = true)
        {
            Tweener<T1,T2> t;
            // Search inside pool
            if (totPooledTweeners > 0) {
                Type type = typeof(T1);
                for (int i = 0; i < totPooledTweeners; ++i) {
                    if (_PooledTweeners[i].type == type) {
                        // Pooled Tweener exists: spawn it
                        t = (Tweener<T1,T2>)_PooledTweeners[i];
                        t.active = true;
                        t.isPlaying = playNow;
                        AddActiveTween(t, updateType);
                        _PooledTweeners.RemoveAt(i);
                        totPooledTweeners--;
                        return t;
                    }
                }
                // Not found: remove a tween from the pool in case it's full
                if (totPooledTweeners >= maxTweeners) {
                    _PooledTweeners.RemoveAt(0);
                    totPooledTweeners--;
                }
            } else {
                // Increase capacity in case max number of Tweeners has already been reached, then continue
                if (totTweeners >= maxTweeners) {
                    if (DOTween.logBehaviour == LogBehaviour.Verbose) Debugger.LogWarning(_MaxTweenersReached);
                    IncreaseCapacities(CapacityIncreaseMode.TweenersOnly);
                }
            }
            // Not found: create new TweenerController
            t = new Tweener<T1,T2>();
            totTweeners++;
            t.active = true;
            t.isPlaying = playNow;
            AddActiveTween(t, updateType);
            return t;
        }

        internal static void Update(float deltaTime)
        {
            DoUpdate(deltaTime * DOTween.timeScale, _ActiveDefaultTweens, UpdateType.Default, totActiveDefaultTweens);
        }

        internal static void FixedUpdate(float deltaTime)
        {
            DoUpdate(deltaTime * DOTween.timeScale, _ActiveFixedTweens, UpdateType.Fixed, totActiveFixedTweens);
        }

        internal static void TimeScaleIndependentUpdate(float deltaTime)
        {
            DoUpdate(deltaTime * DOTween.timeScale, _ActiveIndependentTweens, UpdateType.TimeScaleIndependent, totActiveIndependentTweens);
        }

        // Returns TRUE if the given tween was not already paused
        internal static bool Pause(Tween t)
        {
            if (t.isPlaying) {
                t.isPlaying = false;
                return true;
            }
            return false;
        }

        // Returns TRUE if the given tween was not already playing and is not complete
        internal static bool Play(Tween t)
        {
            if (!t.isPlaying && (!t.isBackwards && !t.isComplete || t.isBackwards && (t.completedLoops > 0 || t.position > 0))) {
                t.isPlaying = true;
                return true;
            }
            return false;
        }

        internal static bool PlayBackwards(Tween t)
        {
            if (!t.isBackwards) {
                t.isBackwards = true;
                Play(t);
                return true;
            }
            return Play(t);
        }

        internal static bool PlayForward(Tween t)
        {
            if (t.isBackwards) {
                t.isBackwards = false;
                Play(t);
                return true;
            }
            return Play(t);
        }

        internal static bool Restart(Tween t)
        {
            Rewind(t);
            t.isPlaying = true;
            return true;
        }

        internal static bool Flip(Tween t)
        {
            t.isBackwards = !t.isBackwards;
            return true;
        }

        internal static bool Rewind(Tween t)
        {
            t.isPlaying = false;
            if (t.position > 0 || t.completedLoops > 0) {
                t.Goto(new UpdateData(0, 0, UpdateMode.Goto));
                return true;
            }
            return false;
        }

        internal static bool Complete(Tween t, bool modifyActiveLists = true)
        {
            if (t.loops == -1) return false;
            if (!t.isComplete) {
                t.Goto(new UpdateData(t.duration, t.loops, UpdateMode.Goto));
                t.isPlaying = false;
                // Despawn if needed
                if (t.autoKill) Despawn(t, modifyActiveLists);
                return true;
            }
            return false;
        }

        // Despawn all
        internal static int DespawnAll()
        {
            int totDespawned = TotActiveTweens();
            if (hasActiveDefaultTweens) {
                DespawnTweens(_ActiveDefaultTweens, false);
                _ActiveDefaultTweens.Clear();
            }
            if (hasActiveFixedTweens) {
                DespawnTweens(_ActiveFixedTweens, false);
                _ActiveFixedTweens.Clear();
            }
            if (hasActiveIndependentTweens) {
                DespawnTweens(_ActiveIndependentTweens, false);
                _ActiveIndependentTweens.Clear();
            }
            hasActiveTweens = hasActiveDefaultTweens = hasActiveFixedTweens = hasActiveIndependentTweens = false;
            totActiveDefaultTweens = totActiveFixedTweens = totActiveIndependentTweens = 0;

            return totDespawned;
        }

        internal static void Despawn(Tween t, bool modifyActiveLists = true)
        {
            switch (t.tweenType) {
            case TweenType.Sequence:
                _PooledSequences.Add(t);
                totPooledSequences++;
                break;
            default:
                _PooledTweeners.Add(t);
                totPooledTweeners++;
                break;
            }
            if (modifyActiveLists) {
                // Remove tween from correct active list
                switch (t.updateType) {
                case UpdateType.Fixed:
                    _ActiveFixedTweens.Remove(t);
                    totActiveFixedTweens--;
                    break;
                case UpdateType.TimeScaleIndependent:
                    _ActiveIndependentTweens.Remove(t);
                    totActiveIndependentTweens--;
                    break;
                default:
                    _ActiveDefaultTweens.Remove(t);
                    totActiveDefaultTweens--;
                    break;
                }
            }
            t.active = false;
            t.Reset();
        }

        internal static int FilteredOperation(OperationType operationType, FilterType filterType, int id, string stringId, UnityEngine.Object unityObjectId)
        {
            int totInvolved = 0;
            if (hasActiveDefaultTweens) {
                totInvolved += DoFilteredOperation(_ActiveDefaultTweens, UpdateType.Default, totActiveDefaultTweens, operationType, filterType, id, stringId, unityObjectId);
            }
            if (hasActiveFixedTweens) {
                totInvolved += DoFilteredOperation(_ActiveFixedTweens, UpdateType.Fixed, totActiveFixedTweens, operationType, filterType, id, stringId, unityObjectId);
            }
            if (hasActiveIndependentTweens) {
                totInvolved += DoFilteredOperation(_ActiveIndependentTweens, UpdateType.TimeScaleIndependent, totActiveIndependentTweens, operationType, filterType, id, stringId, unityObjectId);
            }
            return totInvolved;
        }

        // Destroys any active tween without putting them back in a pool,
        // then purges all pools and resets capacities
        internal static void PurgeAll()
        {
            _ActiveDefaultTweens.Clear();
            _ActiveFixedTweens.Clear();
            _ActiveIndependentTweens.Clear();
            hasActiveTweens = hasActiveDefaultTweens = hasActiveFixedTweens = hasActiveIndependentTweens = false;
            totActiveDefaultTweens = totActiveFixedTweens = totActiveIndependentTweens = 0;
            PurgePools();
            ResetCapacities();
            totTweeners = totSequences = 0;
        }

        // Removes any cached tween from the pools
        internal static void PurgePools()
        {
            totTweeners -= totPooledTweeners;
            totSequences -= totPooledSequences;
            _PooledTweeners.Clear();
            _PooledSequences.Clear();
            totPooledTweeners = totPooledSequences = 0;
        }

        internal static void ResetCapacities()
        {
            SetCapacities(_DefaultMaxTweeners, _DefaultMaxSequences);
        }

        internal static void SetCapacities(int tweenersCapacity, int sequencesCapacity)
        {
            maxTweeners = tweenersCapacity;
            maxSequences = sequencesCapacity;
            _ActiveDefaultTweens.Capacity = tweenersCapacity + sequencesCapacity;
            _ActiveFixedTweens.Capacity = tweenersCapacity + sequencesCapacity;
            _ActiveIndependentTweens.Capacity = tweenersCapacity + sequencesCapacity;
            _PooledTweeners.Capacity = tweenersCapacity;
            _PooledSequences.Capacity = sequencesCapacity;
        }

        internal static UpdateData GetUpdateDataFromDeltaTime(Tween t, float deltaTime)
        {
            deltaTime *= t.timeScale;
            float position = t.position;
            int completedLoops = t.completedLoops;
            if (t.isBackwards) {
                if (completedLoops == t.loops) completedLoops--;
                position -= deltaTime;
                while (position < 0 && completedLoops > 0) {
                    position += t.duration;
                    completedLoops--;
                }
            } else {
                position += deltaTime;
                while (position > t.duration && (t.loops == -1 || completedLoops < t.loops)) {
                    position -= t.duration;
                    completedLoops++;
                }
            }

            return new UpdateData(position, completedLoops);
        }

        internal static int TotActiveTweens()
        {
            return totActiveDefaultTweens + totActiveFixedTweens + totActiveIndependentTweens;
        }

        internal static int TotPooledTweens()
        {
            return totPooledTweeners + totPooledSequences;
        }

        internal static int TotPlayingTweens()
        {
            int tot = 0;
            if (hasActiveDefaultTweens) {
                for (int i = 0; i < totActiveDefaultTweens; ++i) {
                    if (_ActiveDefaultTweens[i].isPlaying) tot++;
                }
            }
            if (hasActiveFixedTweens) {
                for (int i = 0; i < totActiveFixedTweens; ++i) {
                    if (_ActiveFixedTweens[i].isPlaying) tot++;
                }
            }
            if (hasActiveIndependentTweens) {
                for (int i = 0; i < totActiveIndependentTweens; ++i) {
                    if (_ActiveIndependentTweens[i].isPlaying) tot++;
                }
            }
            return tot;
        }

        // ===================================================================================
        // METHODS ---------------------------------------------------------------------------

        static void DoUpdate(float deltaTime, List<Tween> tweens, UpdateType updateType, int totTweens)
        {
            bool willKill = false;
            for (int i = 0; i < totTweens; ++i) {
                Tween t = tweens[i];
                if (t.isPlaying) {
                    if (!t.delayComplete) {
                        deltaTime = t.UpdateDelay(t.elapsedDelay + deltaTime);
                        if (deltaTime <= 0) continue;
                    }
                    bool needsKilling = t.Goto(GetUpdateDataFromDeltaTime(t, deltaTime));
                    if (needsKilling) {
                        t.active = false;
                        willKill = true;
                        _KillList.Add(t);
                        _KillIds.Add(i);
                    }
                }
            }
            // Kill all eventually marked tweens
            if (willKill) {
                DespawnTweens(_KillList, false);
                int count = _KillIds.Count - 1;
                int totRemoved = count + 1;
                for (int i = count; i > -1; --i) tweens.RemoveAt(_KillIds[i]);
                _KillList.Clear();
                _KillIds.Clear();
                switch (updateType) {
                case UpdateType.Fixed:
                    totActiveFixedTweens -= totRemoved;
                    hasActiveFixedTweens = totActiveFixedTweens > 0;
                    break;
                case UpdateType.TimeScaleIndependent:
                    totActiveIndependentTweens -= totRemoved;
                    hasActiveIndependentTweens = totActiveIndependentTweens > 0;
                    break;
                default:
                    totActiveDefaultTweens -= totRemoved;
                    hasActiveDefaultTweens = totActiveDefaultTweens > 0;
                    break;
                }
            }
        }

        static int DoFilteredOperation(List<Tween> tweens, UpdateType updateType, int totTweens, OperationType operationType, FilterType filterType, int id, string stringId, UnityEngine.Object unityObjectId)
        {
            int totInvolved = 0;
            int totDespawned = 0;
            for (int i = totTweens - 1; i > -1; --i) {
                bool isFilterCompliant = false;
                Tween t = tweens[i];
                switch (filterType) {
                case FilterType.All:
                    isFilterCompliant = true;
                    break;
                case FilterType.Id:
                    isFilterCompliant = t.id == id;
                    break;
                case FilterType.StringId:
                    isFilterCompliant = t.stringId == stringId;
                    break;
                case FilterType.UnityObjectId:
                    isFilterCompliant = t.unityObjectId == unityObjectId;
                    break;
                }
                if (isFilterCompliant) {
                    switch (operationType) {
                    case OperationType.Despawn:
                        totInvolved++;
                        Despawn(t, false);
                        tweens.RemoveAt(i);
                        totDespawned++;
                        break;
                    case OperationType.Pause:
                        if (Pause(t)) totInvolved++;
                        break;
                    case OperationType.Play:
                        if (Play(t)) totInvolved++;
                        break;
                    case OperationType.PlayBackwards:
                        if (PlayBackwards(t)) totInvolved++;
                        break;
                    case OperationType.PlayForward:
                        if (PlayForward(t)) totInvolved++;
                        break;
                    case OperationType.Restart:
                        if (Restart(t)) totInvolved++;
                        break;
                    case OperationType.Flip:
                        if (Flip(t)) totInvolved++;
                        break;
                    case OperationType.Rewind:
                        if (Rewind(t)) totInvolved++;
                        break;
                    case OperationType.Complete:
                        bool hasAutoKill = t.autoKill;
                        if (Complete(t, false)) {
                            totInvolved++;
                            if (hasAutoKill) {
                                tweens.RemoveAt(i);
                                totDespawned++;
                            }
                        }
                        break;
                    }
                }
            }
            // Special additional operations in case of despawn
            if (totDespawned > 0) {
                switch (updateType) {
                case UpdateType.Fixed:
                    totActiveFixedTweens -= totDespawned;
                    hasActiveFixedTweens = totActiveFixedTweens > 0;
                    break;
                case UpdateType.TimeScaleIndependent:
                    totActiveIndependentTweens -= totDespawned;
                    hasActiveIndependentTweens = totActiveIndependentTweens > 0;
                    break;
                default:
                    totActiveDefaultTweens -= totDespawned;
                    hasActiveDefaultTweens = totActiveDefaultTweens > 0;
                    break;
                }
            }

            return totInvolved;
        }

        // Adds the given tween to the active tweens list
        static void AddActiveTween(Tween tween, UpdateType updateType)
        {
            tween.updateType = updateType;
            switch (updateType) {
            case UpdateType.Fixed:
                _ActiveFixedTweens.Add(tween);
                hasActiveFixedTweens = true;
                totActiveFixedTweens++;
                break;
            case UpdateType.TimeScaleIndependent:
                _ActiveIndependentTweens.Add(tween);
                hasActiveIndependentTweens = true;
                totActiveIndependentTweens++;
                break;
            default:
                _ActiveDefaultTweens.Add(tween);
                hasActiveDefaultTweens = true;
                totActiveDefaultTweens++;
                break;
            }
            hasActiveTweens = true;
        }

        static void DespawnTweens(List<Tween> tweens, bool modifyActiveLists = true)
        {
            int count = tweens.Count;
            for (int i = 0; i < count; ++i) Despawn(tweens[i], modifyActiveLists);
        }

        static void IncreaseCapacities(CapacityIncreaseMode increaseMode)
        {
            int add = 0;
            switch (increaseMode) {
            case CapacityIncreaseMode.TweenersOnly:
                add += _DefaultMaxTweeners;
                maxTweeners += _DefaultMaxTweeners;
                _PooledTweeners.Capacity += _DefaultMaxTweeners;
                break;
            case CapacityIncreaseMode.SequencesOnly:
                add += _DefaultMaxSequences;
                maxSequences += _DefaultMaxSequences;
                _PooledSequences.Capacity += _DefaultMaxSequences;
                break;
            default:
                add += _DefaultMaxTweeners + _DefaultMaxSequences;
                maxTweeners += _DefaultMaxTweeners;
                maxSequences += _DefaultMaxSequences;
                _PooledTweeners.Capacity += _DefaultMaxTweeners;
                _PooledSequences.Capacity += _DefaultMaxSequences;
                break;
            }
            _ActiveDefaultTweens.Capacity += add;
            _ActiveFixedTweens.Capacity += add;
            _ActiveIndependentTweens.Capacity += add;
        }

        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        // ||| INTERNAL CLASSES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

        internal enum CapacityIncreaseMode
        {
            TweenersAndSequences,
            TweenersOnly,
            SequencesOnly
        }
    }
}