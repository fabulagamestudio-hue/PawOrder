using System;
using System.Collections.Generic;

namespace Iung.Animation
{
    public static class AnimationPropertyController
    {

        public static void ConfigureStartPosition(this List<AnimationProperty> _list)
        {
            foreach (var animation in _list)
            {
                animation.ConfigureStartPosition();
            }
        }

        public static void ConfigureStartPosition(this AnimationProperty[] _list)
        {
            foreach (var animation in _list)
            {
                animation.ConfigureStartPosition();
            }
        }


        public static void ForceStartPosition(this List<AnimationProperty> _list)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                _list[i].ForceStart();
            }
        }

        public static void ForceEndPosition(this List<AnimationProperty> _list)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                _list[i].ForceEnd();
            }
        }


        public static void RevealThis(this List<AnimationProperty> _list, Action OnFinish = null, Action OnStart = null)
        {
            OnStart?.Invoke();
            if (_list.Count == 0)
            {
                OnFinish?.Invoke();
                return;
            }

            for (int i = 0; i < _list.Count; i++)
            {
                _list[i].Show(() =>
                {
                    AllEndedAnimation(_list, OnFinish);
                });
            }
        }

        public static void HideThis(this List<AnimationProperty> _list, Action OnFinish = null)
        {
            if (_list.Count == 0)
            {
                OnFinish?.Invoke();
                return;
            }

            for (int i = 0; i < _list.Count; i++)
            {
                _list[i].Hide(() =>
                {
                    AllEndedAnimation(_list, OnFinish);
                });
            }
        }

        public static bool IsRevealed(this List<AnimationProperty> _list)
        {
            foreach (var animation in _list)
            {
                if (!animation.isRevealed)
                {
                    return false;
                }
            }


            return true;
        }


        public static bool IsAnimating(this List<AnimationProperty> _list)
        {
            bool isAnimating = false;

            foreach (var animation in _list)
            {
                if (animation.isAnimating)
                {
                    isAnimating = true;
                    break;
                }
            }

            return isAnimating;

        }

        static void AllEndedAnimation(List<AnimationProperty> _list, Action OnFinish)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i].isAnimating)
                    return;
            }
            OnFinish?.Invoke();
        }


    }
}
