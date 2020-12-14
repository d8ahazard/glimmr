using System;

namespace Glimmr.Models.ColorSource.Ambient {
    [Serializable]
    public abstract class IScene {
        public enum AnimationMode {
            Linear,
            Reverse,
            Random,
            RandomAll,
            LinearAll
        }

        public enum EasingType {
            FadeOut,
            FadeIn,
            Blend
        }

        private string[] _colors;
        public double AnimationTime { get; protected set; }
        public AnimationMode Mode { get; protected set; }
        public EasingType Easing { get; protected set; }

        public string[] GetColors() {
            return _colors;
        }

        protected void SetColors(string[] value) {
            _colors = value;
        }
    }
}