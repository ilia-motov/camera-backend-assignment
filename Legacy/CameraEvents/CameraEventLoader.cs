using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CameraEvents
{
    public sealed class CameraEventLoader
    {
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
        private readonly ICameraService service;

        public CameraEventLoader(ICameraService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Возвращает уникальные события с Confidence > 0.7 из диапазона [from, to).
        /// Границы и временные метки событий должны быть в UTC; время автоматически не преобразуется.
        /// </summary>
        public List<CameraEvent> LoadEvents(DateTime from, DateTime to)
        {
            if (from.Kind != DateTimeKind.Utc)
                throw new ArgumentException("from must be UTC.", nameof(from));
            if (to.Kind != DateTimeKind.Utc)
                throw new ArgumentException("to must be UTC.", nameof(to));
            if (from > to)
                throw new ArgumentException("from must not be after to.", nameof(from));

            var unique = new Dictionary<(int CameraId, DateTime Timestamp), CameraEvent>();
            var start = from;
            while (start < to)
            {
                var end = to - start <= Window ? to : start.Add(Window);

                var events = service.GetEvents(start, end)
                    ?? throw new InvalidDataException("The camera service returned a null array.");

                foreach (var item in events)
                {
                    if (item == null || item.Timestamp.Kind != DateTimeKind.Utc
                        || double.IsNaN(item.Confidence) || double.IsInfinity(item.Confidence))
                        throw new InvalidDataException("The camera service returned an invalid event.");

                    if (item.Confidence <= 0.7 || item.Timestamp < start || item.Timestamp >= end)
                        continue;

                    var key = (item.CameraId, item.Timestamp);
                    if (!unique.TryGetValue(key, out var previous) || item.Confidence > previous.Confidence)
                    {
                        unique[key] = new CameraEvent
                        {
                            CameraId = item.CameraId,
                            Timestamp = item.Timestamp,
                            Confidence = item.Confidence
                        };
                    }
                }
                start = end;
            }

            return unique.Values.OrderBy(item => item.Timestamp)
                .ThenBy(item => item.CameraId)
                .ToList();
        }
    }
}
