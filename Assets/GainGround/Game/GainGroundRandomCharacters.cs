using System.Collections.Generic;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        CharacterDefinition[] RandomRoster(CharacterDefinition[] original)
        {
            var pool = new List<CharacterDefinition>(SaveHeroCatalog().Values);
            // Sample without replacement within each player's initial party.
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var hero = pool[i]; pool[i] = pool[j]; pool[j] = hero;
            }
            return pool.GetRange(0, Mathf.Min(original.Length, pool.Count)).ToArray();
        }

        CharacterDefinition RandomRescue(CharacterDefinition original)
        {
            // Null slots remain empty; no serialized stage assets are modified.
            if (campaignClassicMode || original == null) return original;
            var pool = new List<CharacterDefinition>(SaveHeroCatalog().Values);
            return pool.Count == 0 ? original : pool[Random.Range(0, pool.Count)];
        }
    }
}
