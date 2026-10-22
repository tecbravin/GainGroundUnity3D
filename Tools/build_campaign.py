"""Reconstruct the 50 Mega Drive map assets from the locally archived VGMaps atlas.

Coordinates are measured in the original 192x224 map pixels, top-left origin.
This is an explicit authoring tool, not an on-import generator. Original input
and the first five hand-authored stage assets are preserved.
"""
from pathlib import Path
import json
from PIL import Image, ImageDraw
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Analysis/StageReferences/GainGround-Rounds.png"
OUT = ROOT / "Assets/GainGround/Editable/OriginalCampaign/Complete"
ATLAS = Image.open(SOURCE).convert("RGB")
STAGES = []

def stage(r, n, title, walls=(), exits=((0, 0, 192, 16),), start=(96, 208)):
    s = dict(round=r, number=n, title=title, walls=[list(w) for w in walls],
             exits=[list(e) for e in exits], start=list(start), enemies=[], captives=[], hazards=[])
    STAGES.append(s)
    return s

def units(s, kind, points, high=False, delay=0):
    for x, y in points:
        s["enemies"].append(dict(kind=kind, x=x, y=y, elevated=high, delay=delay))

def row(s, kind, xs, ys, high=False):
    units(s, kind, [(x,y) for y in ys for x in xs], high)

def rescue(s, name, x, y, players=1):
    s["captives"].append(dict(name=name,x=x,y=y,players=players))

def reinforcement(s, kind, y, count=2):
    for i in range(count):
        units(s, kind, [(8 if i%2==0 else 184,y)], delay=4+i*2)

# Round 1: original early campaign. First five retain their existing gameplay data.
s=stage(1,1,"Planicie"); units(s,"archer",[(18,34),(66,58),(138,50),(42,90),(114,90),(170,106)]); rescue(s,"Glow Knight",72,88)
s=stage(1,2,"Ponte",[(0,120,72,64),(120,120,72,64),(64,32,64,16)])
units(s,"archer",[(18,34),(74,22),(106,22),(170,34),(82,58),(106,58),(42,74),(146,74)])
reinforcement(s,"warrior",192); rescue(s,"Babalu",96,152)
s=stage(1,3,"Rochas",[(0,0,36,160),(88,32,16,128),(152,32,16,128),(104,152,48,16)],[(104,136,48,16)])
row(s,"archer",[12],[26,58,90,122],True); row(s,"warrior",[50,74],[42,90]); units(s,"archer",[(50,138),(74,138),(178,82),(178,122)])
s=stage(1,4,"Fortificacao",[(0,0,64,40),(128,0,64,40)],[(64,0,64,16)])
row(s,"archer",[12,44,148,180],[10],True)
units(s,"warrior",[(76,34),(116,34),(96,58),(72,106),(96,106),(120,106),(50,146),(138,146)])
units(s,"archer",[(50,66),(138,66),(26,98),(166,98),(12,122),(36,122),(156,122),(180,122)])
reinforcement(s,"warrior",152)
s=stage(1,5,"Cerco"); row(s,"archer",[16,56,96,136,176],[34])
units(s,"archer",[(76,66),(116,66),(28,130),(164,130)]); units(s,"warrior",[(36,82),(156,82),(84,122),(108,122)])
reinforcement(s,"warrior",108); rescue(s,"Valkyrie",96,98)
s=stage(1,6,"Paliçada",[(24,0,24,120),(144,0,24,120),(24,120,48,24),(120,120,48,24)],[(48,0,96,16)])
row(s,"archer",[12,180],[34,58,82]); row(s,"archer",[12,36,60,132,156,180],[106])
row(s,"warrior",[54,78,110,134],[30,54]); units(s,"warrior",[(68,82),(124,82),(100,106),(12,130),(180,130),(12,154),(180,154)])
rescue(s,"Gaston",16,16); rescue(s,"Moss",176,16,2)
s=stage(1,7,"Fileiras"); row(s,"archer",[66,122],[26,50,74,98,122,146]); row(s,"archer",[26,162],[42,106]); reinforcement(s,"warrior",140,4)
s=stage(1,8,"Sentinela",exits=[(64,0,64,16)]); row(s,"archer",[52,140],[34,82]); units(s,"demon",[(96,104)])
rescue(s,"Mad Poppy",24,24); reinforcement(s,"warrior",136,4)
s=stage(1,9,"Galerias",[(0,16,64,40),(128,16,64,40),(0,112,48,40),(144,112,48,40)],[(64,0,64,16)])
row(s,"archer",[12,36,156,180],[18,114],True); units(s,"archer",[(84,50),(108,50),(28,90),(164,90)])
row(s,"warrior",[84,108],[74,170]); reinforcement(s,"warrior",80)
s=stage(1,10,"Guardiao de duas cabecas",[(0,0,48,48),(144,0,48,48)],[])
units(s,"dragon",[(96,26)]); row(s,"demon",[24,168],[82,162]); row(s,"archer",[60,84,108,132],[90])

# Round 2: medieval ruins, magic and castle.
s=stage(2,1,"Ruinas",[(24,32,16,16),(64,48,16,16),(120,48,16,16),(160,32,16,16),(32,80,16,16),(88,96,16,16),(48,112,16,16),(128,112,16,16),(8,120,16,16),(168,120,16,16),(88,144,16,16),(40,160,16,16),(144,160,16,16)],[(80,0,32,16)])
units(s,"knight",[(44,16),(164,16),(100,80),(60,96),(132,96),(20,104),(180,104),(12,168),(180,168)])
units(s,"axeman",[(76,24),(124,24),(44,64),(156,64),(100,128),(52,144),(148,144)]); rescue(s,"Moss",16,80)
s=stage(2,2,"Portao do castelo",[(0,0,64,40),(128,0,64,40),(0,112,64,40),(128,112,64,40)],[(64,0,64,16)])
row(s,"knight",[12,36,156,180],[8],True); row(s,"knight",[84,108],[56,80,104])
units(s,"knight",[(12,96),(180,96)]); row(s,"axeman",[12,36,156,180],[48]); row(s,"axeman",[36,156],[72,96])
s=stage(2,3,"Telhados",[(0,72,72,104),(120,0,72,176)],[(0,0,64,16)])
row(s,"knight",[44],[88,144],True); row(s,"knight",[148],[16,56,112,136],True)
row(s,"axeman",[52],[34,58]); reinforcement(s,"knight",184); rescue(s,"Fire Knight",16,40)
s=stage(2,4,"Santuario",[(48,24,16,16),(64,32,16,16),(112,32,16,16),(128,24,16,16),(80,44,32,24)],[(72,0,48,16)])
units(s,"knight",[(12,16),(36,16),(60,8),(84,24),(108,24),(132,8),(156,16),(180,16)])
units(s,"axeman",[(68,74),(124,74)]); row(s,"axeman",[12,36,60,84,108,132,156,180],[112])
s=stage(2,5,"Ponte de pedra",[(0,32,80,64),(112,32,80,64),(42,128,12,24),(138,128,12,24),(32,160,16,16),(148,160,16,16)],[(80,0,32,16)])
row(s,"knight",[20,52,140,172],[8]); row(s,"knight",[20,172],[112])
rescue(s,"Water Knight",16,160); rescue(s,"Kou",176,160,2)
s=stage(2,6,"Sala dos magos",[(0,24,160,8)],[(80,144,32,16)])
row(s,"knight",[36,116,148,180],[8],True); units(s,"knight",[(12,72),(180,72),(28,152),(164,152)])
row(s,"knight",[44,68,92,116,140],[88]); row(s,"wizard",[20,52,84,116,148,172],[52])
units(s,"axeman",[(72,156),(88,172),(104,172),(120,156)])
rescue(s,"Zaemon",16,16); rescue(s,"Kou",56,16); rescue(s,"Honey",76,16,2)
s=stage(2,7,"Corredores",[(0,y,68,16) for y in [0,32,64,96,128,160]]+[(124,y,68,16) for y in [0,64,96,128,160]],[(72,0,48,16)])
units(s,"wizard",[(44,20),(84,20),(108,20),(12,116),(132,148)])
row(s,"knight",[132,156],[48,112]); units(s,"knight",[(12,144),(36,144)]); row(s,"axeman",[132,156,180],[80]); rescue(s,"General",24,24)
s=stage(2,8,"Labirinto de pedra",[(0,32,8,152),(184,32,8,152),(32,64,32,16),(64,48,16,80),(32,104,32,24),(112,48,16,80),(128,64,32,16),(128,104,32,24),(32,144,48,24),(112,144,48,24),(32,168,128,16)],[(72,0,48,16)])
units(s,"slime",[(48,8),(24,32),(80,32),(112,24),(152,32)]); rescue(s,"Professor",88,88)
s=stage(2,9,"Passarelas",[(0,96,96,12),(48,64,16,32),(128,56,64,12),(48,136,144,12),(0,176,144,12)],[(144,0,48,16)])
units(s,"wizard",[(20,20),(20,52)]); units(s,"slime",[(80,24),(112,24),(144,24),(176,24),(112,48),(144,48)])
rescue(s,"Cyber",16,80)
s=stage(2,10,"Cavaleiro gigante",[(0,0,64,40),(128,0,64,40)],[(72,0,48,16)])
units(s,"giant",[(96,38)]); row(s,"knight",[28,164],[8],True); row(s,"wizard",[12,36,156,180],[52])

# Round 3: desert, trenches and eastern fortress.
s=stage(3,1,"Dunas"); row(s,"rifleman",[44,68,92,116],[28]); rescue(s,"Babalu",16,40)
units(s,"worm",[(48,92),(136,132),(88,164)],delay=3)
s=stage(3,2,"Escarpa"); row(s,"gunner",[12,36,84,108,156,180],[44],True); rescue(s,"Moss",40,24)
units(s,"worm",[(40,112),(104,148),(160,180)],delay=4)
s=stage(3,3,"Trincheiras",[(0,16,64,8),(96,16,96,8),(0,104,64,16),(96,104,32,16),(160,104,32,16)],[(0,0,64,16),(128,0,64,16)])
units(s,"rifleman",[(28,36),(60,52),(132,52),(164,36),(76,140),(140,140)])
units(s,"ninja",[(84,10),(108,10),(20,108),(52,108),(116,108),(180,108)])
s=stage(3,4,"Emboscada",[(0,16,64,56),(120,16,72,56)],[(0,0,64,16),(128,0,64,16)])
row(s,"ninja",[12,36,60,132,156,180],[42],True); row(s,"gunner",[60,84,108,132],[84])
row(s,"rifleman",[36,60,132,156],[116]); rescue(s,"Mad Poppy",96,64)
s=stage(3,5,"Jardins",[(0,64,64,88),(120,72,72,16)],[(0,0,192,16)])
units(s,"ninja",[(20,28),(172,36),(132,60),(156,60),(180,60)])
units(s,"bomber",[(52,76),(52,100),(20,124),(52,124)],True)
s=stage(3,6,"Muralha",[(0,0,48,48),(104,0,88,48),(96,72,96,16)],[(48,0,56,16)])
row(s,"bomber",[12,36,124,148,172],[20],True); row(s,"ninja",[12,36,60,84],[76]); reinforcement(s,"rifleman",64)
rescue(s,"Kiddo",56,40)
s=stage(3,7,"Passagem fortificada",[(0,0,24,184),(168,0,24,192),(48,64,96,16),(48,120,96,16)],[(24,0,144,16)])
units(s,"ninja",[(44,28),(148,36),(60,52),(84,52),(108,52),(132,52),(60,108),(84,108),(108,108),(132,108)])
row(s,"bomber",[68,100,132],[84]); rescue(s,"Fire Knight",72,24)
s=stage(3,8,"Ponte elevada",[(0,80,72,32),(120,80,72,32)],[(0,0,48,16),(144,0,48,16)])
units(s,"ninja",[(12,28),(60,20),(132,20),(180,28),(36,60),(68,60),(124,60),(156,60),(28,132),(164,132)])
row(s,"bomber",[36,60,132,156],[92],True)
s=stage(3,9,"Barricadas",[(56,24,80,16),(0,80,48,16),(144,80,48,16)],[(0,0,64,16),(128,0,64,16)])
row(s,"rifleman",[84,108],[12]); row(s,"rifleman",[12,36,156,180],[68])
units(s,"bomber",[(12,44),(180,44)]); units(s,"ninja",[(44,116),(108,116),(172,116),(12,140),(76,140),(140,140)])
rescue(s,"Honey",80,72)
s=stage(3,10,"Dois feiticeiros",[(0,0,48,28),(144,0,48,28),(0,88,48,40),(144,88,48,40)],[])
row(s,"bomber",[12,36,156,180],[12,100],True); units(s,"ninja",[(36,60),(156,60)])
units(s,"sorcererBlue",[(72,34)]); units(s,"sorcererRed",[(124,34)])

# Round 4: modern city.
s=stage(4,1,"Cruzamento",[(92,18,64,28),(94,36,8,44),(90,154,12,54)])
units(s,"soldier",[(140,68),(180,68),(108,100),(148,100),(172,100),(100,132),(132,132),(164,132)])
s=stage(4,2,"Praca",[(0,0,64,52),(128,0,64,52),(104,108,8,48),(152,116,32,56)],[(72,0,48,16)])
row(s,"grenadier",[20,44,148,172],[20],True); row(s,"soldier",[84,108],[36])
row(s,"soldier",[20,44,68,124,148,172],[68]); units(s,"soldier",[(36,100),(156,100)]); rescue(s,"Mamu",176,112)
s=stage(4,3,"Arcade",[(32,40,32,48),(48,88,16,96),(128,40,32,48),(128,88,16,96),(0,96,16,40),(176,96,16,40),(8,152,32,32),(152,152,32,32),(32,0,24,24),(128,0,32,24)],[(0,136,48,16)])
row(s,"streetgun",[84,108],[34,58,82,106,130]); units(s,"brawler",[(20,44),(172,44)])
units(s,"runner",[(20,68),(172,68),(28,108),(164,108)]); rescue(s,"Robbie",168,144)
s=stage(4,4,"Muro urbano",[(0,40,12,32),(40,40,48,32),(104,40,48,32),(180,40,12,32)],[(72,0,48,16)])
row(s,"grenadier",[12,36,60,132,156,180],[20],True)
units(s,"soldier",[(28,68),(164,68),(20,100),(52,108),(76,100),(116,100),(140,108),(172,100),(20,140),(172,140)])
rescue(s,"General",24,80); rescue(s,"Water Knight",72,80); rescue(s,"Professor",136,80,2)
s=stage(4,5,"Viaduto",[(0,0,136,72),(48,120,16,40),(128,120,16,40),(98,144,28,44),(144,128,48,24)],[(144,0,48,16)])
row(s,"grenadier",[12,36,60,84,108],[28],True); row(s,"soldier",[156,180],[44])
row(s,"soldier",[12,36,76],[100,124]); row(s,"grenadier",[156,180],[92])
s=stage(4,6,"Avenida",[(70,156,58,24),(138,116,28,48)],[(144,0,48,16)])
row(s,"grenadier",[84,108],[20,44]); units(s,"grenadier",[(100,76),(52,100),(52,124),(20,116),(20,140)])
row(s,"soldier",[12,36,156,180],[36,60]); row(s,"soldier",[156,180],[84]); rescue(s,"Kou",16,16)
s=stage(4,7,"Carros",[(0,40,68,24),(124,40,68,24),(108,124,28,48),(66,184,62,28)],[(0,0,48,16),(144,0,48,16)])
row(s,"grenadier",[12,36,156,180],[28]); row(s,"soldier",[84,108],[20,44])
units(s,"soldier",[(20,132),(60,132),(84,132),(60,156),(84,156),(172,124),(172,148)])
s=stage(4,8,"Espiral",[(40,40,112,32),(40,72,16,80),(40,152,152,32),(136,96,16,36),(0,56,8,128)],[(72,72,48,16)])
row(s,"grenadier",[12,36,156,180],[8,32],True); row(s,"grenadier",[60,84,108,132],[20],True)
row(s,"soldier",[68,92,116],[108,132]); units(s,"soldier",[(156,132),(180,132)])
s=stage(4,9,"Labirinto industrial",[(0,24,80,28),(112,24,80,28),(72,52,8,28),(112,52,8,28),(32,64,8,40),(152,64,8,40),(32,96,56,28),(104,96,56,28),(88,96,16,80),(0,168,56,24),(136,168,56,24),(48,144,8,24),(136,144,8,24)],[(0,0,48,16),(144,0,48,16)])
row(s,"soldier",[60,84,108,132],[8]); units(s,"soldier",[(60,60),(100,52),(100,76),(132,60),(20,84),(180,84),(20,108),(180,108),(76,140),(116,140),(76,164),(116,164)])
units(s,"grenadier",[(20,60),(180,60),(60,84),(132,84)])
rescue(s,"Valkyrie",24,160); rescue(s,"Betty",168,160,2)
s=stage(4,10,"Tanque de batalha",[(0,0,64,28),(128,0,64,28)],[])
row(s,"soldier",[20,52,84,116,148,180],[92,132]); units(s,"tank",[(96,48)],delay=8)

# Round 5: future, lasers and the defense computer.
s=stage(5,1,"Androides"); row(s,"robot",[28,60,92,124,156],[36])
units(s,"robot",[(76,68),(116,68),(88,100)]); reinforcement(s,"robot",40,8)
s=stage(5,2,"Blindado",[(0,128,64,32),(144,128,48,32)])
row(s,"robot",[20,92,164],[36]); row(s,"turret",[52,100,140],[108]); units(s,"tank",[(96,68)])
s=stage(5,3,"Esferas",[(0,0,80,80),(0,112,112,16),(112,56,16,64)],[(80,0,112,16)])
units(s,"robot",[(92,28),(92,52),(60,92),(84,92)],True)
units(s,"orb",[(144,40),(176,40),(144,96),(176,96),(16,144),(64,152),(128,152),(176,136)])
s=stage(5,4,"Aranhas mecanicas",[(0,0,48,32),(144,0,48,32),(32,160,128,16)],[(48,0,96,16)])
row(s,"robot",[52,76,100,124,148],[44]); units(s,"spider",[(164,58),(144,78),(152,112),(136,128)])
s=stage(5,5,"Nucleo",[(32,48,16,48),(144,48,16,48),(48,104,24,24),(120,104,24,24),(64,128,16,16),(112,128,16,16)],[(80,104,32,16)])
units(s,"robot",[(36,36),(156,36)]); units(s,"orb",[(64,16),(104,32),(120,8),(80,64),(120,80)])
units(s,"turret",[(12,100),(20,124),(44,140),(68,156),(124,156),(148,140),(168,124),(172,100)])
s=stage(5,6,"Formacao final"); row(s,"robot",[28,60,92,124,156],[28]); units(s,"robot",[(44,68),(148,68)])
row(s,"turret",[12,52,92,132,172],[108]); row(s,"turret",[36,92,152],[148])
s=stage(5,7,"Barreiras de energia",[(0,160,64,24),(128,160,64,24)],[(0,0,192,16)])
units(s,"robot",[(100,28)]); row(s,"robot",[12,44,92,148,180],[76]); reinforcement(s,"robot",32,4)
for x,y in [(26,52),(164,52),(52,100),(140,100),(26,132),(164,132)]: units(s,"generator",[(x,y)])
for x,y,w in [(32,42,128),(0,90,48),(144,90,48),(32,122,128)]:
    s["hazards"].append(dict(x=x,y=y,w=w,h=6,kind="laser"))
s=stage(5,8,"Canhoes laterais",[(0,0,24,104),(168,0,24,104)],[(32,0,128,16)])
row(s,"robot",[28,60,132,164],[156]); row(s,"turret",[24,168],[24,48,72,96],True)
s=stage(5,9,"Reator",[(56,32,24,112),(112,32,24,112),(56,144,80,16)],[(80,112,32,16)])
units(s,"orb",[(16,16),(96,16),(168,16),(48,40),(144,40),(16,80),(168,80),(40,120),(152,120),(16,160),(88,168),(168,160)])
units(s,"core",[(96,104)])
s=stage(5,10,"Sistema Gain Ground",[(0,0,64,32),(128,0,64,32)],[])
units(s,"computer",[(96,32)])
for y in [78,110,142]:
    for x in [24,72,120,168]: units(s,"barrier",[(x,y)])

# Actor pivots use a collision clearance of four source pixels.
for s in STAGES:
    key=(s["round"],s["number"])
    if key in ((4,1),(4,7)): s["start"]=[144,208]
    for e in s["enemies"]:
        if key==(1,6) and e["x"] in (36,156) and e["y"]==106: e["elevated"]=True
        if key==(3,3) and e["y"]==108: e["elevated"]=True
        if key==(2,7) and e["y"] in (20,80,112,116,144,148): e["y"]+=6
        if key==(3,7) and e["kind"]=="bomber": e["y"]+=2
        if key==(4,8) and e["x"]==156 and e["y"]==132: e["x"]+=2

# Source rectangles for original enemy sprites, relative to map top-left.
# Each includes only one actor; background palette is removed from its alpha.
SPRITES = {
    "archer": (1,7, (15,32,24,24), (140,176,32,32)),
    "warrior": (1,6,(44,20,22,22),(76,156,40,32)),
    "demon": (1,8,(76,72,44,56),(8,160,48,48)),
    "dragon": (1,10,(48,4,96,48),(0,0,44,44)),
    "knight": (2,1,(32,6,24,28),(8,184,48,32)),
    "axeman": (2,1,(64,12,28,24),(8,184,48,32)),
    "wizard": (2,6,(12,40,20,24),(48,112,48,32)),
    "slime": (2,8,(40,0,16,16),(8,196,48,24)),
    "giant": (2,10,(64,4,64,68),(48,160,48,48)),
    "rifleman": (3,1,(36,16,20,24),(128,160,48,48)),
    "gunner": (3,2,(2,32,20,24),(64,48,16,16)),
    "ninja": (3,4,(2,32,20,24),(80,144,48,48)),
    "bomber": (3,5,(40,64,24,28),(0,64,32,40)),
    "sorcererBlue": (3,10,(48,8,44,48),(56,160,64,48)),
    "sorcererRed": (3,10,(96,8,44,48),(56,160,64,48)),
    "soldier": (4,1,(128,56,24,24),(112,148,48,48)),
    "grenadier": (4,2,(8,8,24,24),(0,0,12,12)),
    "streetgun": (4,3,(72,24,20,24),(72,152,40,40)),
    "brawler": (4,3,(8,32,20,24),(72,152,40,40)),
    "runner": (4,3,(8,56,24,24),(72,152,40,40)),
    "robot": (5,6,(16,16,24,24),(0,176,64,40)),
    "turret": (5,6,(40,96,24,24),(0,176,64,40)),
    "orb": (5,3,(136,32,16,24),(136,56,32,24)),
    "spider": (5,4,(140,44,44,24),(0,112,96,40)),
    "tank": (5,2,(56,48,68,40),(0,64,48,32)),
    "generator": (5,7,(16,40,16,24),(64,56,40,24)),
    "barrier": (5,10,(0,72,48,16),(0,88,32,16)),
    "computer": (5,10,(64,4,64,56),(64,56,64,16)),
    "core": (5,9,(88,96,16,16),(80,128,32,16)),
    # Burrowing creatures are represented by the original desert gunner sprite.
    "worm": (3,2,(2,32,20,24),(64,48,16,16)),
}

def crop_map(r,n):
    x,y=166+324*(n-1),133+228*(r-1)
    return ATLAS.crop((x,y,x+192,y+224))

def crop_rect(im,rect):
    x,y,w,h=rect
    return im.crop((x,y,x+w,y+h))

def export(sprites_only=False):
    (OUT/"Maps").mkdir(parents=True,exist_ok=True)
    (OUT/"Sprites").mkdir(exist_ok=True)
    sprites={}
    for name,(r,n,rect,bg_rect) in SPRITES.items():
        im=crop_map(r,n)
        rgba=np.asarray(crop_rect(im,rect).convert("RGBA")).copy()
        # Preserve original foreground palette; use only repeatedly occurring
        # background colours to avoid deleting highlights from a stray actor.
        bg=np.asarray(crop_rect(im,bg_rect)).reshape(-1,3)
        colors,counts=np.unique(bg,axis=0,return_counts=True)
        background={tuple(c) for c,k in zip(colors,counts) if k>=3}
        for y in range(rgba.shape[0]):
            for x in range(rgba.shape[1]):
                if tuple(rgba[y,x,:3]) in background:
                    rgba[y,x,3]=0
        # Hand-traced cutouts keep neighbouring masonry/carpet out of boss art.
        outlines={
            "dragon":[(3,7),(27,8),(30,0),(45,0),(48,7),(52,0),(66,0),(70,8),(92,7),(87,22),(88,32),(82,43),(66,43),(55,32),(43,32),(29,43),(10,43),(6,32),(8,22)],
            "sorcererBlue":[(0,14),(8,7),(16,6),(24,10),(33,14),(40,21),(43,37),(35,45),(18,42),(9,32),(9,21),(0,21)],
            "sorcererRed":[(0,12),(10,1),(20,0),(27,8),(30,9),(36,15),(39,26),(43,37),(37,44),(25,44),(17,38),(5,38),(0,29)],
            "tank":[(2,15),(11,10),(19,5),(42,5),(52,10),(64,16),(65,27),(56,34),(44,37),(12,37),(1,29)],
        }
        if name in outlines:
            silhouette=Image.new("L",(rgba.shape[1],rgba.shape[0]))
            ImageDraw.Draw(silhouette).polygon(outlines[name],fill=255)
            rgba[:,:,3]=np.minimum(rgba[:,:,3],np.asarray(silhouette))
        if name=="dragon":
            rgb=rgba[:,:,:3].astype(int)
            rgba[:,:,3][(rgb[:,:,1]>0)&(rgb[:,:,1]>=rgb[:,:,0])&(rgb[:,:,1]>=rgb[:,:,2])]=0
        if name=="giant":
            # EXIT lettering behind the helmet is not part of the character.
            rgb=rgba[:12,:,:3].astype(int)
            rgba[:12,:,3][(rgb[:,:,0]>100)&(rgb[:,:,1]>100)&(rgb[:,:,2]<70)]=0
        Image.fromarray(rgba).save(OUT/"Sprites"/(name+".png"))
        sprites[name]=dict(width=rect[2],height=rect[3])
    if sprites_only: return
    for s in STAGES:
        im=crop_map(s["round"],s["number"])
        original=np.asarray(im).copy()
        mask=np.zeros((224,192),dtype=bool)
        def mark(cx,cy,w,h):
            x=max(0,int(cx-w/2)); y=max(0,int(cy-h/2))
            mask[y:min(224,int(cy+h/2)),x:min(192,int(cx+w/2))]=True
        for e in s["enemies"]:
            if e["delay"]>0 or e["kind"] in ("worm",): continue
            # Tank in the fourth round emerges later; it is not in the atlas.
            size=sprites[e["kind"]]
            mark(e["x"],e["y"],size["width"],size["height"])
        for c in s["captives"]: mark(c["x"],c["y"],12,18)
        for h in s["hazards"]:
            mark(h["x"]+h["w"]/2,h["y"]+h["h"]/2,h["w"],h["h"])
        # Texture synthesis only inside actor rectangles. Candidate tiles retain
        # the map's 8px alignment; local colours and proximity choose the fill.
        candidates=[]
        for y in range(0,224,8):
            for x in range(0,192,8):
                if not mask[y:y+8,x:x+8].any() and y>=16:
                    candidates.append((x,y,original[y:y+8,x:x+8]))
        cleaned=original.copy()
        for y in range(0,224,8):
            for x in range(0,192,8):
                m=mask[y:y+8,x:x+8]
                if not m.any(): continue
                target=original[y:y+8,x:x+8]
                region=original[max(0,y-8):min(224,y+16),max(0,x-8):min(192,x+16)]
                region_mask=mask[max(0,y-8):min(224,y+16),max(0,x-8):min(192,x+16)]
                visible=region[~region_mask]
                mean=visible.mean(axis=0) if len(visible) else target.mean(axis=(0,1))
                def score(c):
                    cx,cy,t=c
                    err=np.mean((t.astype(float).mean(axis=(0,1))-mean)**2)
                    if (~m).any(): err+=np.mean((t[~m].astype(float)-target[~m])**2)*2
                    return err+(abs(cx-x)+abs(cy-y))*1.5
                if candidates:
                    tile=min(candidates,key=score)[2]
                    cleaned[y:y+8,x:x+8][m]=tile[m]
        Image.fromarray(cleaned).save(OUT/"Maps"/f'{s["round"]}-{s["number"]:02}.png')
    serialized=json.loads(json.dumps(STAGES))
    for item in serialized:
        for key in ("walls","exits"):
            item[key]=[dict(zip(("x","y","w","h"),a)) for a in item[key]]
        item["start"]=dict(zip(("x","y"),item["start"]))
    manifest=dict(source="VGMaps / Gain Ground (Mega Drive), G.E.R.",stages=serialized,sprites=[
        dict(name=k,**v) for k,v in sprites.items()])
    (OUT/"campaign.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
    sheet=Image.new("RGB",(10*192,5*248),(20,20,28))
    draw=ImageDraw.Draw(sheet)
    for s in STAGES:
        x=(s["number"]-1)*192; y=(s["round"]-1)*248
        draw.text((x+4,y+4),f'{s["round"]}-{s["number"]}',fill="white")
        sheet.paste(Image.open(OUT/"Maps"/f'{s["round"]}-{s["number"]:02}.png'),(x,y+24))
    sheet.save(ROOT/"Validation/CampaignTerrain.png")
    print(f"Authored {len(STAGES)} stages, {sum(len(s['enemies']) for s in STAGES)} enemies, "
          f"{sum(len(s['captives']) for s in STAGES)} captives and {len(sprites)} sprite extracts.")

if __name__=="__main__":
    export()
