using System;

namespace Glimmr.Models.CaptureSource.Ambient.Scenes {
    [Serializable]
    public abstract class SceneBase {
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

        private string[] colors;
        public double AnimationTime { get; protected set; }
        public AnimationMode Mode { get; protected set; }
        public EasingType Easing { get; protected set; }

        public string[] GetColors() {
            return colors;
        }

        protected void SetColors(string[] value) {
            colors = value;
        }
    }
}