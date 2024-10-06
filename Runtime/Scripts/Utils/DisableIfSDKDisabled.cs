using System;
using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    [AttributeUsage(AttributeTargets.Field)]
    public class DisableIfSDKDisabled : PropertyAttribute
    {
    }
}