﻿// Author: Daniele Giardini - http://www.demigiant.com
// Created: 2014/09/01 18:50
// 
// License Copyright (c) Daniele Giardini.
// This work is subject to the terms at http://dotween.demigiant.com/license.php

using UnityEngine;

#pragma warning disable 1591
namespace DG.Tweening.Plugins.Options
{
    public struct QuaternionOptions
    {
        public bool beyond360; // If FALSE uses shortest route with rotations (like Unity does), otherwise calculates them fully
        internal AxisConstraint axisConstraint; // Used by SpecialStartupMode SetLookAt
        internal Vector3 up; // Used by SpecialStartupMode SetLookAt
    }
}