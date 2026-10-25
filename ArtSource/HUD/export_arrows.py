"""Export the editable SVG arrows as antialiased transparent Unity sprites."""

from pathlib import Path
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw


SOURCE = Path(__file__).resolve().parent
DESTINATION = SOURCE.parents[1] / "Assets" / "GainGround" / "Resources" / "HUD"
SIZE = 256
SUPERSAMPLE = 4


for name in ("ArrowLeft", "ArrowRight"):
    root = ET.parse(SOURCE / f"{name}.svg").getroot()
    canvas = Image.new("RGBA", (SIZE * SUPERSAMPLE, SIZE * SUPERSAMPLE))
    draw = ImageDraw.Draw(canvas)
    for polygon in root.iter("{http://www.w3.org/2000/svg}polygon"):
        points = [tuple(map(float, pair.split(","))) for pair in polygon.attrib["points"].split()]
        if name == "ArrowRight":
            points = [(SIZE - x, y) for x, y in points]
        draw.polygon([(round(x * SUPERSAMPLE), round(y * SUPERSAMPLE)) for x, y in points],
                     fill=polygon.attrib["fill"])
    canvas.resize((SIZE, SIZE), Image.Resampling.LANCZOS).save(DESTINATION / f"{name}.png")
