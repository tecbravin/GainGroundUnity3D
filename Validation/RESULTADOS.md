# Verificação — 09/10/2026 — campanha 1-1 a 1-5

- Unity **2022.3.62f1**: compilação C# concluída.
- **18/18 testes Unity aprovados**, sem falhas ou testes ignorados. Evidência: `EditMode.xml` e `Tests.log`.
- Cobertura: decodificadores, colisões, posições dos atores, caminhos até as cinco saídas e os três resgates, projéteis, arqueiros elevados, reforços pendentes, pausa, derrota, recuperação de companheiros e progressão das cinco fases até a vitória.
- Os testes conferem que a cena contém cinco fases e três personagens iniciais. O cenário de regressão de perda/resgate usa uma equipe reduzida deliberadamente.
- **Build Windows x64 concluído**: `Builds/Windows/GainGround.exe`. `Build.log` registra `Build Finished, Result: Success.`.
- `Stage_1_1.png` a `Stage_1_5.png`: capturas da câmera da Unity, inspecionadas visualmente. O recorte do cabeçalho EXIT no 1-2 foi corrigido após a primeira inspeção.
- Fontes, coordenadas e limites da adaptação: `Analysis/StageReferences/ESTAGIOS.md`.

## Limites

Os testes entram em Play Mode e manipulam posições/estado para cobrir fluxos; não representam uma partida humana completa por teclado. As capturas da câmera não incluem a interface IMGUI. O executável foi compilado; a validação automatizada de gameplay foi realizada na Unity. Não houve comparação quadro a quadro com o Mega Drive.

As cinco fases reproduzem a estrutura dos mapas de referência, com terreno recortado do atlas e colisões aproximadas. Sprites móveis, armas, IA, parâmetros e tempos de reforço continuam adaptados. Não há animações ou áudio originais. Os testes Python das rotinas da ROM não foram repetidos nesta alteração de fases.
