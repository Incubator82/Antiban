using System;
using System.Collections.Generic;
using System.Linq;

namespace Antiban
{
    public class Antiban
    {
        public EventMessage[] EventMessages { get; private set; }
        public int LengthEventMessages { get; private set; }
        public readonly int PeriodBetweenMessagesToDifferentNumbers = 10;
        public readonly int PeriodBetweenMessagesToOneNumber = 60;
        public readonly int PeriodBetweenMessagesWithPriorityOne = 24;

        public Antiban()
        {
            LengthEventMessages = 0;
            EventMessages = new EventMessage[0];
        }
        /// <summary>
        /// Добавление сообщений в систему, для обработки порядка сообщений
        /// </summary>
        /// <param name="eventMessage"></param>
        public void PushEventMessage(EventMessage eventMessage)
        {
            var tempArray = EventMessages;

            EventMessages = new EventMessage[++LengthEventMessages];
            EventMessages[^1] = eventMessage;

            for (int i = 0; i < tempArray.Length; i++)
                EventMessages[i] = tempArray[i];
        }

        /// <summary>
        /// Вовзращает порядок отправок сообщений
        /// </summary>
        /// <returns></returns>
        public List<AntibanResult> GetResult()
        {
            var result = new List<AntibanResult>();

            // Создаем буфер сообщений для работы с датами
            var eventMessages_buffer = (Array.ConvertAll(EventMessages, x => new EventMessageDto
            (
                x.Id,
                x.Phone,
                x.DateTime,        // Время возникновения события
                x.Priority,        // Приоритет сообщения
                null               // Предполагаемое время отправки сообщения
            ))).ToList();

            // Заволняем выходной результат
            var resultEditForDateByTheRules = eventMessages_buffer.Select(EditForDateByTheRules).ToList();
            foreach (var editDateMessage in resultEditForDateByTheRules)
                result.Add(new AntibanResult() { EventMessageId = editDateMessage.Id, SentDateTime = editDateMessage.SentDateTime ?? DateTime.MinValue });


            // Метод работы с датами, по установленным правилам в тестовом задании
            EventMessageDto EditForDateByTheRules(EventMessageDto em)
            {
                // Последнее отработанное сообщение
                var lastEventMessageRow = eventMessages_buffer.Where(w => w.Phone == em.Phone && w.DateTime < em.DateTime).MaxBy(obd => obd.SentDateTime);

                // Первый заход записей у каждого номера
                if (lastEventMessageRow == null)
                {
                    // Находим записи в диапазоне 10 секунд для расчета периода между сообщениями
                    var sec10 = (from ems in eventMessages_buffer
                                 where ems.DateTime < em.DateTime && ems.DateTime > em.DateTime.AddSeconds(-PeriodBetweenMessagesToDifferentNumbers)
                                 group ems by ems.Phone into g
                                 select g).ToList();

                    DateTime dt = em.DateTime;
                    // добавляем необходимый период между сообщениями на разные номера
                    TimeSpan ts = new TimeSpan(dt.Hour, dt.Minute, sec10.Count * PeriodBetweenMessagesToDifferentNumbers);
                    dt = dt.Date + ts;

                    // Редактируем тукущее поле SentDateTime 
                    em.SentDateTime = dt;
                    return em;
                }
                else
                {
                    // Логика работы с датами в одной минуте
                    if ((em.DateTime - lastEventMessageRow.SentDateTime)?.TotalSeconds <= PeriodBetweenMessagesToOneNumber)
                    {
                        if (lastEventMessageRow.SentDateTime <= em.DateTime)
                            em.SentDateTime = lastEventMessageRow.SentDateTime?.AddSeconds(PeriodBetweenMessagesToOneNumber);
                        else
                        {
                            var oneMinute = eventMessages_buffer
                                .Where(x => x.Phone == em.Phone && x.SentDateTime >= lastEventMessageRow.DateTime.AddSeconds(-PeriodBetweenMessagesToOneNumber)
                                && x.SentDateTime <= lastEventMessageRow.DateTime).MaxBy(mb => mb.SentDateTime);
                            em.SentDateTime = oneMinute != null ? oneMinute.SentDateTime?.AddSeconds(PeriodBetweenMessagesToOneNumber) : lastEventMessageRow.SentDateTime;
                        }
                    }

                    // Добавляем при необходимости +24 часа
                    if (em.Priority == 1 && lastEventMessageRow.Priority != 0)
                    {
                        var lastEventMessageRowPriority = eventMessages_buffer.Where(w => w.Phone == em.Phone && w.DateTime < em.DateTime && w.Priority == 1)
                                                                              .OrderByDescending(obd => obd.DateTime).ToList();
                        var hours24 = lastEventMessageRowPriority.Where(x => x.SentDateTime >= em.DateTime.AddHours(-PeriodBetweenMessagesWithPriorityOne));

                        if (hours24.Any())
                        {
                            var lastSentDate = hours24.MaxBy(r => r.SentDateTime);

                            em.SentDateTime = lastSentDate.SentDateTime.Value.AddHours(PeriodBetweenMessagesWithPriorityOne);
                        }
                    }
                    if (em.SentDateTime == null)
                        em.SentDateTime = em.DateTime;
                }

                return em;
            }

            // Сортируем данные по вновь отработынным датам
            result = result.OrderBy(ob => ob.SentDateTime).ToList();

            return result;
        }
    }

    public class EventMessageDto : EventMessage
    {
        public DateTime? SentDateTime { get; set; }
        public EventMessageDto(int id, string phone, DateTime dateTime, int priority, DateTime? sentDateTime) : base(id, phone, dateTime, priority)
        {
            SentDateTime = sentDateTime;
        }
    }

    //новая ветка
    // ветка 2
}
