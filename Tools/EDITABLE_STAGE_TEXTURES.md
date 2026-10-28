# Texturas editáveis dos estágios

Cada `Assets/GainGround/Editable/OriginalCampaign/Stage_*.asset` mostra no Inspector
um atalho para sua paleta em `Assets/GainGround/Editor/StageTexturePalettes`.
As 50 paletas expõem:

- **Floor**: atlas original do chão daquele estágio.
- **Elevations**: textura aplicada aos retângulos elevados/paredes da fase.
- **Constructions**: atlas original de construções e objetos do cenário.
- **Exit Banner**: textura compartilhada da saída.
- **Objects**: prefabs efetivamente posicionados naquela fase. Abra o prefab
  vinculado e altere seu `SpriteRenderer.sprite` para mudar um objeto isolado.

As imagens fonte ficam em `Assets/GainGround/Editable/OriginalCampaign/StageTextures`.
Substitua um PNG mantendo seu nome e dimensões, ou atribua outro `Texture2D` na
paleta. Para aplicar alterações ao chão, às elevações ou à faixa EXIT, clique em
**Recompor mapa deste estágio** no Inspector da paleta (ou **Recompor PNG do mapa**
no Inspector do estágio). O comando requer Python com Pillow instalado. Ele altera
somente o PNG final em `Complete/Maps` da fase escolhida e o reimporta na Unity.
O mapa e sua prévia usam esse PNG. Construções posicionadas usam os sprites de
seus prefabs, por isso a troca do atlas Constructions não substitui
automaticamente os sprites recortados desses prefabs.

As paletas ficam em `Editor` e não entram na build. O jogo continua carregando
somente o PNG final e os prefabs posicionados na fase. O script de extração
`Tools/materialize_editable_stage_textures.py` preserva imagens, paletas e GUIDs
já existentes ao ser executado novamente.
