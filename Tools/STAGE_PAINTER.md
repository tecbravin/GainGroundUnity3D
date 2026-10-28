# Editor de estágios

Abra **Gain Ground > Editor de estágios**, escolha um `StageDefinition` e clique
em **Criar layout**. O layout é um prefab em `Editable/OriginalCampaign/PaintedStages`.
O mapa atual aparece como guia, e a pintura ocorre na Scene View em Prefab Mode.

O estágio **1-1** já foi convertido: `Stage_1_1_Layout.prefab` contém 168 tiles
individuais que reproduzem exatamente a imagem anterior. Abra a fase no editor
ou selecione esse prefab para editar seu chão. As posições do início, da saída,
do resgate e dos inimigos podem ser movidas pelo botão **Mover início, saída,
resgates e inimigos**. A arte original continua na fase até você aplicar uma
mudança pelo botão de aplicação do layout.

1. Clique em **Preparar tiles dos atlas da fase** para recortar os atlas da
   `StageTexturePalette` em células de 64×64. Os PNGs e `Tile` assets criados
   podem ser editados individualmente; o comando preserva arquivos já criados.
   Também é possível atribuir um `Tile` próprio diretamente ao campo **Pincel**.
2. Escolha um tile e uma camada. **Chão**, **Elevações**, **Decoração** e
   **Efeitos** compõem a imagem do mapa. **Bloqueio** define a colisão e aparece
   em vermelho somente no editor. Pinte com o botão esquerdo; use **Apagar**
   para remover células. Desative **Pintar na Scene View** para selecionar e
   mover objetos com as ferramentas normais da Unity.
3. Os objetos e perigos já presentes na fase aparecem como marcadores com
   prévia visual. Use **Adicionar objeto** ou **Adicionar perigo** para criar
   outros. O marcador é movido na
   Scene View e suas propriedades são alteradas no Inspector. Objetos aceitam
   prefab, vida destrutível e grupo de teleporte; perigos aceitam prefab ou
   sprite e IDs dos controladores. Para efeitos animados, coloque um prefab.
4. Clique em **Aplicar pintura, colisões e objetos à fase**. O editor gera um
   PNG em `PaintedMaps`, atualiza `background`, combina as células de bloqueio
   em retângulos e sincroniza os marcadores com `objects` e `hazards` da fase.

O PNG é composto sempre sobre o mapa de origem guardado no prefab, portanto
apagar uma pintura e aplicar novamente a remove do resultado. Objetos e perigos
anteriores que não foram criados pelo editor permanecem no asset. A câmera, o
seletor de estágio e o build WebGL continuam lendo o `StageDefinition` e seu PNG
final; os prefabs e tiles de autoria não são carregados durante a partida.

Para ver uma mudança no jogo, aplique o layout e execute a fase. Alterar apenas
o prefab de autoria não modifica o mapa usado pelo runtime.
