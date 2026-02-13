using System;
using System.Collections.Generic;
using System.Linq; // <- añadido
using Cliente.Models;

namespace Cliente.Services
{
    /// <summary>
    /// Motor de combate 1vs1 por equipos, estilo Pokémon simplificado.
    /// </summary>
    public sealed class BattleEngine
    {
        private readonly Pokemon[] _teamA;
        private readonly Pokemon[] _teamB;

        public Pokemon[] TeamA => _teamA;
        public Pokemon[] TeamB => _teamB;

        private int _indexA;
        private int _indexB;

        public Pokemon? ActiveA => _indexA < _teamA.Length ? _teamA[_indexA] : null;
        public Pokemon? ActiveB => _indexB < _teamB.Length ? _teamB[_indexB] : null;

        private const int DefaultLevel = 50;
        private readonly Random _rng = Random.Shared;

        public BattleEngine(Pokemon[] teamA, Pokemon[] teamB)
        {
            _teamA = teamA ?? Array.Empty<Pokemon>();
            _teamB = teamB ?? Array.Empty<Pokemon>();
            _indexA = 0;
            _indexB = 0;
        }

        // Borroka amaitu den adierazten du
        public bool IsFinished => ActiveA == null || ActiveB == null;

        // Irabazlearen izena bueltatzen du, borroka amaituta badago
        public string? WinnerName(string playerAName, string playerBName)
        {
            if (!IsFinished) return null;
            return ActiveA == null ? playerBName : playerAName;
        }

        // Talde bakoitzean zenbat Pokemon dauden bizirik
        public int AliveCountTeamA() => _teamA.Count(p => (p.HP ?? 0) > 0);
        public int AliveCountTeamB() => _teamB.Count(p => (p.HP ?? 0) > 0);

        // Borroka-txanda bat exekutatzen du eta emaitza bueltatzen du
        public TurnResult PlayTurn(string playerAName, string playerBName, int moveSlotA, int moveSlotB)
        {
            AdvanceTeamA();
            AdvanceTeamB();

            if (IsFinished)
                return new TurnResult("El combate ya terminó.", Array.Empty<TurnAction>(), ActiveA, ActiveB);

            var a = ActiveA!;
            var b = ActiveB!;

            int speedA = a.Speed ?? 0;
            int speedB = b.Speed ?? 0;

            // Zein Pokemonek erasotzen duen lehenengo erabakitzen du (abiadura edo ausaz)
            bool aFirst = speedA > speedB || (speedA == speedB && _rng.Next(2) == 0);
            var actions = new List<TurnAction>(2);

            // Erasoaren kalkulua eta aplikazioa
            void DoAttack(Pokemon attacker, Pokemon defender, bool attackerIsA, int moveSlot)
            {
                if ((attacker.HP ?? 0) <= 0) return;
                if ((defender.HP ?? 0) <= 0) return;

                var (moveName, basePower, category, moveType) = GetMove(attacker, moveSlot);

                if (string.IsNullOrWhiteSpace(moveName) || basePower <= 0)
                {
                    actions.Add(new TurnAction(
                        attackerIsA ? playerAName : playerBName,
                        attacker.Name ?? "(sin nombre)",
                        moveName ?? "(sin movimiento)",
                        0,
                        attackerIsA ? playerBName : playerAName,
                        defender.Name ?? "(sin nombre)",
                        defender.HP ?? 0,
                        (defender.HP ?? 0) <= 0));
                    return;
                }

                // Fisikoa edo berezia den erasoaren arabera estatistikak aukeratzen dira
                bool isPhysical = string.Equals(category, "物理", StringComparison.OrdinalIgnoreCase);
                bool isSpecial = string.Equals(category, "特殊", StringComparison.OrdinalIgnoreCase);
                if (!isPhysical && !isSpecial) isPhysical = true;

                int atkStat = isPhysical ? (attacker.Attack ?? 0) : (attacker.SpAttack ?? 0);
                int defStat = isPhysical ? (defender.Defense ?? 0) : (defender.SpDefense ?? 0);

                atkStat = Math.Max(atkStat, 1);
                defStat = Math.Max(defStat, 1);

                // Kaltearen formula sinplifikatua
                double levelTerm = (2.0 * DefaultLevel) / 5.0 + 2.0;
                double baseDamage = ((levelTerm * basePower * atkStat / defStat) / 50.0) + 2.0;

                double stab = HasStab(attacker, moveType) ? 1.5 : 1.0;
                double typeEffectiveness = GetTypeEffectiveness(moveType, defender);

                int damage = Math.Max(1, (int)Math.Floor(baseDamage * stab * typeEffectiveness));

                defender.HP = Math.Max(0, (defender.HP ?? 0) - damage);

                actions.Add(new TurnAction(
                    attackerIsA ? playerAName : playerBName,
                    attacker.Name ?? "(sin nombre)",
                    moveName,
                    damage,
                    attackerIsA ? playerBName : playerAName,
                    defender.Name ?? "(sin nombre)",
                    defender.HP ?? 0,
                    defender.HP == 0));
            }

            // Txandaren ordena: lehenengo erasotzailea eta bigarrena
            if (aFirst)
            {
                DoAttack(a, b, true, moveSlotA);
                DoAttack(b, a, false, moveSlotB);
            }
            else
            {
                DoAttack(b, a, false, moveSlotB);
                DoAttack(a, b, true, moveSlotA);
            }

            AdvanceTeamA();
            AdvanceTeamB();

            return new TurnResult(
                aFirst ? $"{playerAName} ataca primero." : $"{playerBName} ataca primero.",
                actions,
                ActiveA,
                ActiveB);
        }

        // Pokemon baten mugimendua eta bere propietateak lortzen ditu
        private static (string name, int power, string category, string moveType) GetMove(Pokemon p, int slot)
        {
            return slot switch
            {
                1 => (p.Move1 ?? "Move1", p.Move1Power ?? 0, p.Move1Category ?? "", p.Move1Type ?? ""),
                2 => (p.Move2 ?? "Move2", p.Move2Power ?? 0, p.Move2Category ?? "", p.Move2Type ?? ""),
                3 => (p.Move3 ?? "Move3", p.Move3Power ?? 0, p.Move3Category ?? "", p.Move3Type ?? ""),
                4 => (p.Move4 ?? "Move4", p.Move4Power ?? 0, p.Move4Category ?? "", p.Move4Type ?? ""),
                _ => ("(mov inválido)", 0, "", "")
            };
        }

        // Hurrengo bizirik dagoen Pokemonera pasatzen da (talde A)
        private void AdvanceTeamA()
        {
            while (_indexA < _teamA.Length && (_teamA[_indexA].HP ?? 0) <= 0)
                _indexA++;
        }

        // Hurrengo bizirik dagoen Pokemonera pasatzen da (talde B)
        private void AdvanceTeamB()
        {
            while (_indexB < _teamB.Length && (_teamB[_indexB].HP ?? 0) <= 0)
                _indexB++;
        }

        // STAB (Same Type Attack Bonus) kalkulatzen du
        private static bool HasStab(Pokemon attacker, string moveType)
        {
            if (string.IsNullOrWhiteSpace(moveType) || string.IsNullOrWhiteSpace(attacker.Type))
                return false;

            var types = attacker.Type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var t in types)
                if (string.Equals(t, moveType, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        // ================= POKEMONEN TIPOEN TAULA =================

        // Erasoaren eraginkortasuna kalkulatzen du tipoen arabera
        private static double GetTypeEffectiveness(string moveType, Pokemon defender)
        {
            if (string.IsNullOrWhiteSpace(moveType) || string.IsNullOrWhiteSpace(defender.Type))
                return 1.0;

            var defTypes = defender.Type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            double multiplier = 1.0;

            foreach (var defType in defTypes)
                multiplier *= GetEffectivenessForSingleType(moveType, defType);

            return multiplier;
        }

        // Tipoen arteko eraginkortasun taula
        private static readonly Dictionary<string, Dictionary<string, double>> TypeChart =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Normal"] = new() { ["Rock"] = 0.5, ["Steel"] = 0.5, ["Ghost"] = 0 },
                ["Fire"] = new() { ["Grass"] = 2, ["Ice"] = 2, ["Bug"] = 2, ["Steel"] = 2, ["Fire"] = 0.5, ["Water"] = 0.5, ["Rock"] = 0.5, ["Dragon"] = 0.5 },
                ["Water"] = new() { ["Fire"] = 2, ["Ground"] = 2, ["Rock"] = 2, ["Water"] = 0.5, ["Grass"] = 0.5, ["Dragon"] = 0.5 },
                ["Grass"] = new() { ["Water"] = 2, ["Ground"] = 2, ["Rock"] = 2, ["Fire"] = 0.5, ["Grass"] = 0.5, ["Poison"] = 0.5, ["Flying"] = 0.5, ["Bug"] = 0.5, ["Dragon"] = 0.5, ["Steel"] = 0.5 },
                ["Electric"] = new() { ["Water"] = 2, ["Flying"] = 2, ["Electric"] = 0.5, ["Grass"] = 0.5, ["Dragon"] = 0.5, ["Ground"] = 0 },
                ["Ice"] = new() { ["Grass"] = 2, ["Ground"] = 2, ["Flying"] = 2, ["Dragon"] = 2, ["Fire"] = 0.5, ["Water"] = 0.5, ["Ice"] = 0.5, ["Steel"] = 0.5 },
                ["Fighting"] = new() { ["Normal"] = 2, ["Ice"] = 2, ["Rock"] = 2, ["Dark"] = 2, ["Steel"] = 2, ["Poison"] = 0.5, ["Flying"] = 0.5, ["Psychic"] = 0.5, ["Bug"] = 0.5, ["Fairy"] = 0.5, ["Ghost"] = 0 },
                ["Poison"] = new() { ["Grass"] = 2, ["Fairy"] = 2, ["Poison"] = 0.5, ["Ground"] = 0.5, ["Rock"] = 0.5, ["Ghost"] = 0.5, ["Steel"] = 0 },
                ["Ground"] = new() { ["Fire"] = 2, ["Electric"] = 2, ["Poison"] = 2, ["Rock"] = 2, ["Steel"] = 2, ["Grass"] = 0.5, ["Bug"] = 0.5, ["Flying"] = 0 },
                ["Flying"] = new() { ["Grass"] = 2, ["Fighting"] = 2, ["Bug"] = 2, ["Electric"] = 0.5, ["Rock"] = 0.5, ["Steel"] = 0.5 },
                ["Psychic"] = new() { ["Fighting"] = 2, ["Poison"] = 2, ["Psychic"] = 0.5, ["Steel"] = 0.5, ["Dark"] = 0 },
                ["Bug"] = new() { ["Grass"] = 2, ["Psychic"] = 2, ["Dark"] = 2, ["Fire"] = 0.5, ["Fighting"] = 0.5, ["Poison"] = 0.5, ["Flying"] = 0.5, ["Ghost"] = 0.5, ["Steel"] = 0.5, ["Fairy"] = 0.5 },
                ["Rock"] = new() { ["Fire"] = 2, ["Ice"] = 2, ["Flying"] = 2, ["Bug"] = 2, ["Fighting"] = 0.5, ["Ground"] = 0.5, ["Steel"] = 0.5 },
                ["Ghost"] = new() { ["Psychic"] = 2, ["Ghost"] = 2, ["Dark"] = 0.5, ["Normal"] = 0 },
                ["Dragon"] = new() { ["Dragon"] = 2, ["Steel"] = 0.5, ["Fairy"] = 0 },
                ["Dark"] = new() { ["Psychic"] = 2, ["Ghost"] = 2, ["Fighting"] = 0.5, ["Dark"] = 0.5, ["Fairy"] = 0.5 },
                ["Steel"] = new() { ["Ice"] = 2, ["Rock"] = 2, ["Fairy"] = 2, ["Fire"] = 0.5, ["Water"] = 0.5, ["Electric"] = 0.5, ["Steel"] = 0.5 },
                ["Fairy"] = new() { ["Fighting"] = 2, ["Dragon"] = 2, ["Dark"] = 2, ["Fire"] = 0.5, ["Poison"] = 0.5, ["Steel"] = 0.5 }
            };

        // Tipo bakarreko eraginkortasuna kalkulatzen du
        private static double GetEffectivenessForSingleType(string moveType, string targetType)
        {
            if (!TypeChart.TryGetValue(moveType, out var dict))
                return 1.0;

            return dict.TryGetValue(targetType, out var mult) ? mult : 1.0;
        }
    }

    // Txanda baten emaitza gordetzen duen erregistroa
    public sealed record TurnResult(
        string Info,
        IReadOnlyList<TurnAction> Actions,
        Pokemon? ActiveA,
        Pokemon? ActiveB);

    // Ekintza bakoitzaren informazioa gordetzen duen erregistroa
    public sealed record TurnAction(
        string AttackerPlayer,
        string AttackerPokemon,
        string MoveName,
        int MovePower,
        string DefenderPlayer,
        string DefenderPokemon,
        int DefenderHpAfter,
        bool DefenderFainted);
}