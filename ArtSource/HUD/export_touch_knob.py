"""Export the HUD touch knob artwork as a transparent, antialiased PNG."""

from pathlib import Path

from PIL import Image, ImageDraw


size = 512
scale = 3
center = size // 2
canvas = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
draw = ImageDraw.Draw(canvas)


def circle(radius, color):
    box = (center - radius, center - radius, center + radius, center + radius)
    draw.ellipse(tuple(round(value * scale) for value in box), fill=color)


for radius, color in (
    (236, "#352312"), (226, "#986326"), (216, "#e6bd69"),
    (205, "#ffdf98"), (195, "#795029"), (183, "#20282a"),
    (174, "#596464"), (163, "#303a3d"), (144, "#394447"),
):
    circle(radius, color)


def arc(radius, start, end, color, width):
    box = (center - radius, center - radius, center + radius, center + radius)
    draw.arc(tuple(round(value * scale) for value in box), start, end,
             fill=color, width=width * scale)


arc(171, 198, 285, "#a8b2a6", 10)
arc(174, 25, 140, "#161e21", 10)
arc(205, 215, 293, "#fff0ba", 9)

output = Path(__file__).resolve().parents[2] / "Assets/GainGround/Resources/HUD/TouchKnob.png"
canvas.resize((size, size), Image.Resampling.LANCZOS).save(output)
