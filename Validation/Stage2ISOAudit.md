# Revisão do segundo mundo contra a ISO

Fonte: `fr2s01.map` a `fr2s10.map` e os atlas `StageTextures/2-XX/Floor.png` extraídos da ISO. O script `Tools/repair_stage2_iso_floor.py` lê as coordenadas UV desses mapas e atualiza os tiles editáveis sem mudar seus GUIDs.

| Fase | Piso | Resultado |
|---|---|---|
| 2-1 | Pedra cinza | Padrão atual compatível com o piso da ISO; mantido. |
| 2-2 | Grama | Piso compatível; muralhas de castelo editáveis mantidas. |
| 2-3 | Grama | Piso compatível; casinhas com telhado azul mantidas. |
| 2-4 | Pedra clara | Substituído o gramado pelo piso da ISO; a colina, a saída e suas bordas foram preservadas. |
| 2-5 | Grama e portão de pedra | Mantido o gramado contínuo e o portão aberto solicitados anteriormente; a malha de altura da ISO tem uma faixa elevada que ainda não foi reconstruída no layout. |
| 2-6 | Piso avermelhado | Substituído o xadrez cinza pelo piso da ISO; saída e barreira superior preservadas. |
| 2-7 | Pedra escura com faixas | Piso e faces das 11 paredes usam recortes editáveis do mapa 2-07; a saída foi preservada. |
| 2-8 | Terra laranja | Piso atual compatível; mantido. |
| 2-9 | Pedra azul | Piso atual compatível; mantido. |
| 2-10 | Pedra clara | Piso atual compatível; mantido. |

Checagens estáticas: nas dez fases, o ponto inicial alcança a saída e os inimigos comuns não nascem dentro das colisões. Nas fases 2-4, 2-6 e 2-7, os 168 tiles PNG de cada uma reproduzem pixel a pixel a prévia, e as referências de sprites dos Tile assets permanecem válidas. O gerador do piso é idempotente.

Os modelos nativos e registros de atores da ISO ainda não oferecem uma associação confiável entre tipo, instância e prefab em todas as fases. As paredes e posições de inimigos dessas fases não foram substituídas por aproximações. A conferência visual e de combate em Play Mode permanece pendente.
