using System;
using UnityEngine;

namespace GainGround.Game
{
    [Serializable]
    public sealed class EndingCard
    {
        [TextArea(2, 5)] public string text;
        [Tooltip("Imagem opcional exibida acima do texto.")]
        public Sprite image;
        [Min(.1f), Tooltip("Tempo em que o cartão fica totalmente visível, sem contar os fades.")]
        public float holdSeconds = 6f;
        [Range(.5f, 2f)] public float fontScale = 1f;
        [Tooltip("Permite ajustar as áreas da imagem e do texto em proporções da tela (0 a 1).")]
        public bool customLayout;
        public Rect imageArea = new Rect(.15f, .08f, .7f, .52f);
        public Rect textArea = new Rect(.1f, .64f, .8f, .26f);
    }

    [CreateAssetMenu(menuName = "Gain Ground/Sequência final")]
    public sealed class GainGroundEndingSequence : ScriptableObject
    {
        [Min(.05f), Tooltip("Duração de cada fade in e fade out entre cartões.")]
        public float fadeSeconds = .65f;
        [Min(.05f)] public float finalMessageFadeSeconds = .65f;
        public EndingCard[] cards = Array.Empty<EndingCard>();
    }
}
