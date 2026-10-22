# Validação — personagens originais

Data: 20/10/2026. Estado: pronto com limitações de fidelidade descritas abaixo.

## Evidências

- Unity 2022.3.62f1 compilou os scripts e serializou os 20 perfis.
- 54 testes passaram, zero falhas: os 30 anteriores e 24 novos casos.
- Os testes com `EnterPlayMode` exercitaram disparo normal, especial e dash
  dos 20 personagens; limites separados de projéteis de P1/P2; pausa;
  colisão com paredes; acerto de alvos elevados; explosão de granada;
  congelamento e retomada de movimento; dano periódico; ricochete;
  retorno de bumerangue; cancelamento dos orbes na troca.
- Referências da cena: 20 personagens únicos, todos existentes em Characters.
- Capturas dos 20 perfis foram renderizadas no Unity e inspecionadas na imagem
  `OriginalWeapons.png`.
- `PlayableProjectBuilder.BuildWindows` terminou com código 0 e resultado
  de build Succeeded (o método lança exceção para qualquer outro resultado).
  Artefatos foram copiados para `Builds/Windows`.

Comandos: `Tools/Validate-Unity.ps1 -Step Prepare`, `-Step Tests`, `-Step Build`.
Resultados: `EditMode.xml`, `Tests.log`, `Build.log`.
Baseline anterior preservado em `BeforeOriginalCharacters-EditMode.xml`.

## Falhas investigadas

A primeira tentativa de importação no projeto principal encontrou o projeto
aberto em outra instância. A importação e a validação foram realizadas na cópia
isolada `.validation/UnityProject`; nenhum Editor do usuário foi fechado.

Primeira execução: 53/54. O teste de descongelamento usava um inimigo de ataque
à distância já dentro da distância em que ele para de caminhar. O cenário foi
corrigido para um inimigo corpo a corpo; a lógica de descongelamento não foi
alterada para fazer o teste passar. Evidência em `OriginalCharacters-FirstRun.xml`.
Última execução: 54/54.

## Limitações

Não houve comparação quadro a quadro com a ROM. Números de balanceamento e
trajetórias são adaptações à escala Unity. Personagens usam os sprites da
recriação; os projéteis têm novas silhuetas geradas por código.
Detalhes e fontes em `Analysis/PERSONAGENS.md`.

A versão WebGL publicada anteriormente não foi reconstruída ou republicada
nesta alteração. A validação de runtime foi feita no Editor, incluindo co-op
local em uma única instância; não foi alegada uma sessão manual completa de jogo
no executável Windows.
