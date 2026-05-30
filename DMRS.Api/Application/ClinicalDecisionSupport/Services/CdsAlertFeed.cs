using System.Collections.Concurrent;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed record CdsAlertEvent(
        string Id,
        string PatientId,
        string Hook,
        string CardSummary,
        string Indicator,
        DateTimeOffset FiredAt);

    /// <summary>
    /// Singleton in-memory ring buffer of the most recent CDS card fire events.
    /// Capped at 100 entries — oldest entries are dropped when the cap is hit.
    /// </summary>
    public sealed class CdsAlertFeed
    {
        private const int MaxEntries = 100;
        private readonly ConcurrentQueue<CdsAlertEvent> _queue = new();

        public void Enqueue(string patientId, string hook, IEnumerable<CdsCard> cards)
        {
            foreach (var card in cards)
            {
                _queue.Enqueue(new CdsAlertEvent(
                    Guid.NewGuid().ToString("N"),
                    patientId,
                    hook,
                    card.Summary,
                    card.Indicator,
                    DateTimeOffset.UtcNow));

                // Trim to cap
                while (_queue.Count > MaxEntries)
                    _queue.TryDequeue(out _);
            }
        }

        /// <summary>Returns up to <paramref name="count"/> most-recent events, newest first.</summary>
        public IReadOnlyList<CdsAlertEvent> GetRecent(int count = 20)
            => [.. _queue.Reverse().Take(count)];
    }
}
