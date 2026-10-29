# Comparação das prefabs de inimigos com a ISO de PS2

Data: 2026-10-28  
ISO consultada: `Sega Ages Vol. 9 Gain Ground.iso`  
Escopo: 32 prefabs em `Assets/GainGround/Editable/Characters/Models3D/EnemyActors`.

## Resultado

Os vínculos dos 32 atores às malhas/modelos convertidos da ISO estão presentes. A auditoria da campanha verificou 50 fases, 695 referências de inimigos e 30 mapeamentos de arquétipos à ISO, sem erros. `RunningKnight` é uma variante de gameplay que usa intencionalmente o sprite e o modelo do cavaleiro; essa exceção agora é reconhecida pelo verificador.

Os recursos da ISO disponíveis para esta comparação contêm modelos, esqueletos, clipes e texturas de efeitos. Eles não fornecem uma tabela de HP, IA ou física de ragdoll legível pelo projeto. Portanto, não é possível afirmar paridade desses parâmetros só confrontando os assets. A campanha usa regras de gameplay próprias, reaproveitadas nos equivalentes visuais PS2.

## Inventário das prefabs

`Clipes` é a contagem do modelo ISO associado; os índices de repouso/movimento/ataque usados no jogo são 0/1/2. `Projétil visual` é a referência configurada na prefab, se houver. A associação visual dos tiros não prova, por si só, que velocidade, cadência, dano e trajetória reproduzam o comportamento do PS2.

| Prefab | Modelo da ISO | Clipes | Projétil visual |
|---|---|---:|---|
| archer | enemy01_yumi | 5 | Arrow |
| axeman | enemy01_ken02 | 5 | sprite Spear |
| barrier | enemy04_4st_07 | 0 | — |
| bomber | enemy03_bomb | 5 | Bomb |
| brawler | enemy03_nife1 | 5 | — |
| computer | enemy08a_r8b | 8 | BlueBolt |
| core | enemy04_4_07a | 0 | BlueBolt |
| demon | enemy01_zou_h | 6 | Fireball_00 |
| dragon | enemy01a_r1b_b | 4 | Fireball_01 |
| generator | enemy04_4_06b | 0 | — |
| giant | enemy02a_r2b | 5 | RedOrb |
| grenadier | enemy03_bomb | 5 | Bomb |
| gunner | enemy03_gun2 | 7 | WhiteBolt |
| knight | enemy02_knight1 | 5 | — |
| MiniSlime | enemy02_slime | 5 | — |
| ninja | enemy01_nife1 | 5 | — |
| orb | enemy04_4_05a | 5 | BlueOrb |
| rifleman | enemy03_gun1 | 7 | WhiteBolt |
| robot | enemy04_4_01a | 5 | BlueBolt |
| runner | enemy03_nife1 | 5 | — |
| RunningKnight | enemy02_knight1 | 5 | — |
| slime | enemy02_slime | 5 | — |
| soldier | enemy02_kensi1 | 5 | WhiteBolt |
| sorcererBlue | enemy03a_r3bb | 0 | BlueOrb |
| sorcererRed | enemy03a_r3br | 5 | RedOrb |
| spider | enemy04_4_03a | 8 | GreenOrb |
| streetgun | enemy03_kasa1 | 5 | WhiteBolt |
| tank | enemy04a_r4b | 6 | RedBolt |
| turret | enemy04_4_04a | 5 | RedBolt |
| warrior | enemy01_ken01 | 5 | — |
| wizard | enemy02_magic | 5 | PurpleOrb |
| worm | enemy03_musi01 | 7 | GreenOrb |

## HP, comportamento, animação e ragdoll

- **HP:** não é armazenado nos modelos ISO. O HP vem de `EnemySpawn` nos assets de fase e pode receber padrões de `EnemyPrefabDefaults`. `slime` e `MiniSlime` têm padrões próprios; `RunningKnight` também define HP e velocidade. Os valores das fases não foram substituídos porque a ISO não oferece uma tabela confiável para compará-los.
- **Comportamento:** também vem da campanha/código Unity, não do modelo convertido. A documentação da conversão já identifica os comportamentos como regras da campanha Mega Drive. Não há base nos recursos atuais para substituir IA por uma suposta regra PS2.
- **Animações:** os modelos com clipes têm animações preservadas; quatro modelos desta lista não têm clipes próprios (`barrier`, `core`, `generator` e `sorcererBlue`). Esses atores continuam estáticos, em acordo com os dados de origem. Os índices 0/1/2 continuam sendo a atribuição da adaptação; os arquivos nomeiam as ações como `Anime####`, então não confirmam semanticamente nem quadro a quadro essas atribuições.
- **Ajuste feito:** `PS2EnemyVisual.Attack()` agora mantém o ataque ativo pela duração do clipe 2 fornecida pelo modelo, em frames a 60 fps. Em atores compostos, usa a maior duração válida entre as partes. Se o modelo não possui esse clipe, mantém o fallback anterior de 0,45 s.
- **Morte/ragdoll:** o modelo do gigante (`enemy02a_r2b`) tem o clipe 4, `Anime0004`, com 120 frames. Seguindo a identificação do usuário de que este é o movimento em que ele cai e deixa a espada no chão, a prefab `giant` agora toca esse clipe uma vez ao morrer e mantém a pose final por 2 s antes de remover o visual. A prefab não usa física de ragdoll (`Rigidbody`/juntas); é a animação de queda original do modelo. Outros inimigos continuam sem um clipe de morte semanticamente identificado.
- **Projéteis:** as referências configuradas nas prefabs foram inventariadas acima. A ISO tem texturas nos pacotes `weapon.rom` e `effect.rom`, parcialmente extraídas em `Assets/GainGround/Editable/PS2Effects`, com proveniência registrada em `provenance.json`. Não há nos assets extraídos um mapeamento confirmado para cada ataque inimigo nem os dados completos de trajetória/dano; esses valores permanecem da implementação de campanha.

## Verificação executada

`python Tools/audit_ps2_enemy_assets.py` — 50 fases, 695 inimigos, 30 mapeamentos de modelo à ISO, zero erros. O relatório numérico está em `Validation/PS2EnemyAssetAudit.json`. A auditoria confirma referências de assets e hashes de modelos; ela não demonstra equivalência de tabelas de spawn, HP, IA, dano ou física PS2.
