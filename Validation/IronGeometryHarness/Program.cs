using System;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame { }
}

internal static class Program
{
    static int Main()
    {
        foreach (var size in new[] { new Vector2(400, 48), new Vector2(46, 60), new Vector2(88, 62), new Vector2(60, 44) })
        {
            var bounds = new Rect(13, 27, size.x, size.y);
            var pieces = GainGround.Game.GainGroundGame.IronFrameRects(bounds);
            if (pieces.Length != 9) throw new Exception("Expected nine pieces");
            float area = 0;
            for (int i = 0; i < pieces.Length; i++)
            {
                var part = pieces[i];
                if (part.width <= 0 || part.height <= 0 || part.xMin < bounds.xMin || part.yMin < bounds.yMin ||
                    part.xMax > bounds.xMax || part.yMax > bounds.yMax)
                    throw new Exception("Piece outside button: " + size);
                area += part.width * part.height;
                for (int j = 0; j < i; j++)
                {
                    var other = pieces[j];
                    float overlapWidth = Math.Min(part.xMax, other.xMax) - Math.Max(part.xMin, other.xMin);
                    float overlapHeight = Math.Min(part.yMax, other.yMax) - Math.Max(part.yMin, other.yMin);
                    if (overlapWidth > .001f && overlapHeight > .001f)
                        throw new Exception("Overlapping pieces: " + size);
                }
            }
            if (Math.Abs(area - bounds.width * bounds.height) > .02f ||
                Math.Abs(pieces[0].xMin - bounds.xMin) > .001f ||
                Math.Abs(pieces[0].yMin - bounds.yMin) > .001f ||
                Math.Abs(pieces[8].xMax - bounds.xMax) > .001f ||
                Math.Abs(pieces[8].yMax - bounds.yMax) > .001f ||
                pieces[0].width > Math.Min(size.x * .22f, size.y * .42f) + .001f ||
                pieces[0].height > size.y * .38f + .001f)
                throw new Exception("Pieces do not close the rectangle: " + size);
            Console.WriteLine("PASS " + size);
        }
        return 0;
    }
}
