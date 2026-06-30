using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using System.Threading.Tasks;
using System.Linq;

namespace GameAuthAPI.Hubs
{
    public class BattleHub : Hub
    {
        private readonly GameDbContext _context;

        public BattleHub(GameDbContext context)
        {
            _context = context;
        }

        // ===================== PvE (Игрок vs Моб) =====================

        public async Task AttackMob(int playerId, int mobId, int? skillId = null)
        {
            var player = await _context.Players
                .Include(p => p.PlayerItems)
                    .ThenInclude(pi => pi.Item)
                .Include(p => p.PlayerSkills)
                    .ThenInclude(ps => ps.Skill)
                .FirstOrDefaultAsync(p => p.Id == playerId);

            if (player == null)
            {
                await Clients.Caller.SendAsync("BattleError", "Игрок не найден.");
                return;
            }

            var mob = await _context.Mobs
                .Include(m => m.SpawnLocation)
                .FirstOrDefaultAsync(m => m.Id == mobId);

            if (mob == null)
            {
                await Clients.Caller.SendAsync("BattleError", "Моб не найден.");
                return;
            }

            if (mob.Health <= 0)
            {
                await Clients.Caller.SendAsync("BattleError", "Моб уже мёртв.");
                return;
            }

            var damage = CalculateDamage(player, mob, skillId);
            mob.Health -= damage;

            if (mob.Health <= 0)
            {
                mob.Health = 0;
                await _context.SaveChangesAsync();

                player.Coins += 10 + mob.Health / 10;
                player.PlayerKills++;

                await _context.SaveChangesAsync();

                await Clients.All.SendAsync("MobDefeated", mobId, playerId, player.Name);
                await Clients.Caller.SendAsync("BattleResult", $"Моб {mob.Name} убит! Получено {10 + mob.Health / 10} монет.");
                return;
            }

            await _context.SaveChangesAsync();

            await Clients.All.SendAsync("ReceiveDamage", mobId, damage, mob.Health);
            await Clients.Caller.SendAsync("BattleResult", $"Вы нанесли {damage} урона мобу {mob.Name}. Осталось здоровья: {mob.Health}");
        }

        // ===================== PvP (Игрок vs Игрок) =====================

        /// <summary>
        /// Вызов на дуэль
        /// </summary>
        public async Task ChallengeDuel(int challengerId, int opponentId)
        {
            var challenger = await _context.Players.FindAsync(challengerId);
            var opponent = await _context.Players.FindAsync(opponentId);

            if (challenger == null || opponent == null)
            {
                await Clients.Caller.SendAsync("BattleError", "Игрок не найден.");
                return;
            }

            // Проверяем, нет ли уже активной дуэли
            var existingDuel = await _context.Duels
                .FirstOrDefaultAsync(d =>
                    (d.ChallengerId == challengerId && d.OpponentId == opponentId) ||
                    (d.ChallengerId == opponentId && d.OpponentId == challengerId) &&
                    (d.Status == DuelStatus.Pending || d.Status == DuelStatus.Active));

            if (existingDuel != null)
            {
                await Clients.Caller.SendAsync("BattleError", "У вас уже есть активная дуэль с этим игроком.");
                return;
            }

            var duel = new Duel
            {
                ChallengerId = challengerId,
                OpponentId = opponentId,
                Status = DuelStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Duels.Add(duel);
            await _context.SaveChangesAsync();

            await Clients.User(opponentId.ToString()).SendAsync("DuelChallenge", challengerId, challenger.Name);
            await Clients.Caller.SendAsync("DuelSent", $"Вы вызвали {opponent.Name} на дуэль.");
        }

        /// <summary>
        /// Принятие дуэли
        /// </summary>
        public async Task AcceptDuel(int duelId, int playerId)
        {
            var duel = await _context.Duels
                .Include(d => d.Challenger)
                .Include(d => d.Opponent)
                .FirstOrDefaultAsync(d => d.Id == duelId);

            if (duel == null)
            {
                await Clients.Caller.SendAsync("BattleError", "Дуэль не найдена.");
                return;
            }

            if (duel.OpponentId != playerId)
            {
                await Clients.Caller.SendAsync("BattleError", "Вы не являетесь участником этой дуэли.");
                return;
            }

            if (duel.Status != DuelStatus.Pending)
            {
                await Clients.Caller.SendAsync("BattleError", "Дуэль уже неактивна.");
                return;
            }

            duel.Status = DuelStatus.Active;
            duel.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await Clients.User(duel.ChallengerId.ToString()).SendAsync("DuelStarted", duelId, duel.OpponentId, duel.Opponent.Name);
            await Clients.User(duel.OpponentId.ToString()).SendAsync("DuelStarted", duelId, duel.ChallengerId, duel.Challenger.Name);
            await Clients.Caller.SendAsync("BattleResult", "Дуэль началась!");
        }

        /// <summary>
        /// Отказ от дуэли
        /// </summary>
        public async Task RejectDuel(int duelId, int playerId)
        {
            var duel = await _context.Duels.FindAsync(duelId);
            if (duel == null)
            {
                await Clients.Caller.SendAsync("BattleError", "Дуэль не найдена.");
                return;
            }

            if (duel.OpponentId != playerId)
            {
                await Clients.Caller.SendAsync("BattleError", "Вы не являетесь участником этой дуэли.");
                return;
            }

            duel.Status = DuelStatus.Cancelled;
            await _context.SaveChangesAsync();

            await Clients.User(duel.ChallengerId.ToString()).SendAsync("DuelRejected", playerId);
            await Clients.Caller.SendAsync("BattleResult", "Вы отклонили дуэль.");
        }

        /// <summary>
        /// Атака игрока в PvP
        /// </summary>
        public async Task AttackPlayer(int attackerId, int defenderId, int duelId, int? skillId = null)
        {
            var duel = await _context.Duels
                .FirstOrDefaultAsync(d => d.Id == duelId && d.Status == DuelStatus.Active);

            if (duel == null)
            {
                await Clients.Caller.SendAsync("BattleError", "Дуэль не найдена или неактивна.");
                return;
            }

            if (duel.ChallengerId != attackerId && duel.OpponentId != attackerId)
            {
                await Clients.Caller.SendAsync("BattleError", "Вы не участвуете в этой дуэли.");
                return;
            }

            var attacker = await _context.Players
                .Include(p => p.PlayerItems)
                    .ThenInclude(pi => pi.Item)
                .FirstOrDefaultAsync(p => p.Id == attackerId);

            var defender = await _context.Players
                .Include(p => p.PlayerItems)
                    .ThenInclude(pi => pi.Item)
                .FirstOrDefaultAsync(p => p.Id == defenderId);

            if (attacker == null || defender == null)
            {
                await Clients.Caller.SendAsync("BattleError", "Игрок не найден.");
                return;
            }

            var damage = CalculatePlayerDamage(attacker, defender, skillId);

            await Clients.User(attackerId.ToString()).SendAsync("PvPAttackResult", defenderId, damage);
            await Clients.User(defenderId.ToString()).SendAsync("PvPDefendResult", attackerId, damage);
            await Clients.All.SendAsync("PvPDamage", duelId, attackerId, defenderId, damage);
        }

        /// <summary>
        /// Завершение дуэли
        /// </summary>
        public async Task FinishDuel(int duelId, int winnerId, int loserId)
        {
            var duel = await _context.Duels.FindAsync(duelId);
            if (duel == null || duel.Status != DuelStatus.Active)
            {
                await Clients.Caller.SendAsync("BattleError", "Дуэль не найдена или неактивна.");
                return;
            }

            duel.Status = DuelStatus.Finished;
            duel.WinnerId = winnerId;
            duel.FinishedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var winner = await _context.Players.FindAsync(winnerId);
            var loser = await _context.Players.FindAsync(loserId);

            if (winner != null)
            {
                winner.PvP_Wins++;
                winner.PvP_Kills++;
                winner.Coins += 50;
            }

            if (loser != null)
            {
                loser.PvP_Losses++;
                loser.PvP_Deaths++;
            }

            await _context.SaveChangesAsync();

            await Clients.User(winnerId.ToString()).SendAsync("DuelFinished", "Победа!", true);
            await Clients.User(loserId.ToString()).SendAsync("DuelFinished", "Поражение...", false);
            await Clients.All.SendAsync("DuelResult", duelId, winnerId, loserId);
        }

        // ===================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ =====================

        private int CalculateDamage(Player player, Mob mob, int? skillId = null)
        {
            var playerStats = player.CalculateTotalStats();
            var attackPower = playerStats.ContainsKey("AttackPower") ? (int)playerStats["AttackPower"] : 10;

            if (skillId.HasValue)
            {
                var skill = _context.Skills.FirstOrDefault(s => s.Id == skillId.Value);
                if (skill != null)
                {
                    attackPower += skill.Damage;
                }
            }

            var defense = mob.Health / 10 + 5;
            var damage = attackPower - defense;
            return damage > 0 ? damage : 1;
        }

        private int CalculatePlayerDamage(Player attacker, Player defender, int? skillId = null)
        {
            var attackerStats = attacker.CalculateTotalStats();
            var defenderStats = defender.CalculateTotalStats();

            var attackPower = attackerStats.ContainsKey("AttackPower") ? (int)attackerStats["AttackPower"] : 10;
            var defense = defenderStats.ContainsKey("Defense") ? (int)defenderStats["Defense"] : 5;

            if (skillId.HasValue)
            {
                var skill = _context.Skills.FirstOrDefault(s => s.Id == skillId.Value);
                if (skill != null)
                {
                    attackPower += skill.Damage;
                }
            }

            var damage = attackPower - defense;
            return damage > 0 ? damage : 1;
        }
    }
}