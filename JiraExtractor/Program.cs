using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;
using  JiraApiOpenSourseLibrary.JiraRestClient;
using JiraApiOpenSourseLibrary;
using RestSharp.Extensions;


namespace JiraExtractor
{
    internal class Program
    {
        public static void Main()
        {
            //этот код создаёт и конфигурирует службу, используется Topshelf

            HostFactory.Run(x =>
            {
                x.Service<Prog>(s =>
                {
                    s.ConstructUsing(name => new Prog()); //создаём службу из класса Prog
                    s.WhenStarted(tc => tc.Start()); //говорим, какой метод будет при старте службы
                    s.WhenStopped(tc => tc.Stop()); //говорим, какой метод выполнится при остановке службы
                });
                x.RunAsNetworkService(); //указываем свойства службы
                x.SetDescription("Service for JIRA. Extracts all tickets in database");
                x.SetDisplayName("JiraExtractor");
                x.SetServiceName("JiraExtractor");
                x.StartAutomaticallyDelayed();
            });
        }
    }

    internal class Prog
    {
        private Parametr jiraParam; //адрес jira с которой работаем
        private Parametr userLoginParam; //под кем будет ходить Бот при мониторинге жира во время дежурств (логин)
        private Parametr userPasswordParam; //под кем будет ходить Бот при мониторинге жира во время дежурств (пароль) 
        private Parametr intervalParam; //как насколько делаем паузы при опросе jira

        public void Start() //метод вызывается при старте службы
        {
            try
            {
                try //пишем в лог о запуске службы
                {
                    using (var repository = new Repository<DbContext>())
                    {
                        var logReccord = new Log
                        {
                            Date = DateTime.Now,
                            MessageTipe = "info",
                            Operation = "StartService",
                            Exception = ""
                        };
                        repository.Create(logReccord);
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(60000);
                        // еcли не доступна БД и не получается залогировать запуск, ждём 60 секунд и пробуем еще раз.
                    using (var repository = new Repository<DbContext>())
                        //использую репозиторий для работы с БД, какая будет БД указано в DbContext
                    {
                        repository.Create(new Log //создаю объект Log и пишу его в БД
                        {
                            Date = DateTime.Now,
                            MessageTipe = "error",
                            Operation = "StartService",
                            Exception = ex.GetType() + ": " + ex.Message
                        });
                        repository.Create(new Log
                        {
                            Date = DateTime.Now,
                            MessageTipe = "info",
                            Operation = "StartService2Attemp",
                            Exception = ""
                        });
                    }

                }

                using (var repository = new Repository<DbContext>()) //инициализирую парамтры приложения из БД
                {
                    jiraParam = repository.Get<Parametr>(p => p.Name == "jira");  //ссылка на jira
                    userLoginParam = repository.Get<Parametr>(p => p.Name == "dafaultuserlogin"); //логин
                    userPasswordParam = repository.Get<Parametr>(p => p.Name == "dafaultuserpassword"); //пароль
                    intervalParam = repository.Get<Parametr>(p => p.Name == "interval"); //пауза между запросами в jira
                }

                //создаю и запускаю задачу, чтобы в бесконечном цикле будет проверять jira 
                Task tsk = new Task(CheckJira);
                tsk.Start();
               

            }
            catch (Exception ex)
            {
                using (var repository = new Repository<DbContext>())
                {
                    repository.Create(new Log
                    {
                        Date = DateTime.Now,
                        MessageTipe = "fatal",
                        Operation = "StartService",
                        Exception = ex.GetType() + ": " + ex.Message
                    });

                }
            }
        }

        public void Stop() //метод вызывается при остановке службы
        {
            try
            {
                using (var repository = new Repository<DbContext>())
                {
                    repository.Create(new Log
                    {
                        Date = DateTime.Now,
                        MessageTipe = "info",
                        Operation = "StopService",
                        Exception = "",
                    });
                }
                Thread.ResetAbort();
            }
            catch (Exception ex)
            {
                using (var repository = new Repository<DbContext>())
                {
                    repository.Create(new Log
                    {
                        Date = DateTime.Now,
                        MessageTipe = "fatal",
                        Operation = "StopService",
                        Exception = ex.GetType() + ": " + ex.Message
                    });
                }
            }
        }

        public void CheckJira()
        {
            using (var repository = new Repository<DbContext>()) //создаю репозиторий для работы с БД
            {
                try
                {
                    var jira = new JiraClient(jiraParam.ValueString, userLoginParam.ValueString, userPasswordParam.ValueString); //объявляю клиент jira
                    while (true)
                    {
                        var lastProcessedChangeDateParameter =
                            repository.Get<Parametr>(p => p.Name == "processedchangedate");
                        var lastProcessedChangeDate = lastProcessedChangeDateParameter.ValueDate; //до какой даты изменения вычитаны
                        var endCheckChangeDate = lastProcessedChangeDate.AddHours(1); //за сколько часов будем вычитывать изменения

                        if (endCheckChangeDate > System.DateTime.Now)  
                        {
                            endCheckChangeDate = System.DateTime.Now;
                        }


                        try
                        {
                            var issuses =
                                jira.EnumerateIssuesByQuery(
                                    "project = SUPPORT AND updated >= \"" + lastProcessedChangeDate.ToString("yyyy-MM-dd HH:mm") + "\" AND updated <= \""
                                    + endCheckChangeDate.ToString("yyyy-MM-dd HH:mm") + "\" ORDER BY key", null, 0).ToList();  //получил тикеты
                            
                            foreach (var issue in issuses)
                            {
                                // вычитываю SLA. Проверяю на null, если так, то делаю null
                                var assignIssueFailed = issue.fields.customfield_13041.completedCycles.FirstOrDefault() == null? (bool?) null
                                    : issue.fields.customfield_13041.completedCycles.FirstOrDefault().breached; 

                                var assignTime = issue.fields.customfield_13041.completedCycles.FirstOrDefault() == null ? (long?)null
                                    : issue.fields.customfield_13041.completedCycles.FirstOrDefault().elapsedTime.millis;

                                var slaTakeInWorkFailed = issue.fields.customfield_13042.completedCycles.FirstOrDefault() == null ? (bool?)null
                                    : issue.fields.customfield_13042.completedCycles.FirstOrDefault().breached;

                                var takeInWorkTime = issue.fields.customfield_13042.completedCycles.FirstOrDefault() == null ? (long?)null
                                    : issue.fields.customfield_13042.completedCycles.FirstOrDefault().elapsedTime.millis;


                                //этот SLA циклический, поэтому вычитываю все значения в цикле.
                                //Если хоть раз нарушен, то считаем нарушенным
                                //Время - среднее значение
                                bool? slaWaitForAnswer = null;
                                long? waitForAnswerTime = null;

                                var slaWaitForAnswerList = issue.fields.customfield_13941.completedCycles;
                                foreach (JiraApiOpenSourseLibrary.CompletedCycle slaCompletedCycle in slaWaitForAnswerList)
                                {
                                    slaWaitForAnswer = slaWaitForAnswer != null ? slaWaitForAnswer & slaCompletedCycle.breached : slaCompletedCycle.breached;
                                    waitForAnswerTime = waitForAnswerTime != null
                                        ? waitForAnswerTime + slaCompletedCycle.elapsedTime.millis
                                        : slaCompletedCycle.elapsedTime.millis;
                                }
                                if(waitForAnswerTime != null) { waitForAnswerTime /= slaWaitForAnswerList.Count;}
                                //обрабатываю тип тикета
                                TicketType ticketTypeFromDb = null;
                                if (issue.fields.issuetype != null)
                                {
                                    ticketTypeFromDb =
                                        repository.Get<TicketType>(
                                            ticketType => ticketType.id == issue.fields.issuetype.id);
                                    if (ticketTypeFromDb == null)
                                    {
                                        ticketTypeFromDb = (new TicketType()
                                        {
                                            id = issue.fields.issuetype.id,
                                            name = issue.fields.issuetype.name
                                        });
                                        repository.Create(ticketTypeFromDb);
                                    }
                                }
                                //обрабатываю тип статус тикета
                                Status statusFromDb = null;
                                if (issue.fields.status != null)
                                {
                                    statusFromDb = repository.Get<Status>(status => status.id == issue.fields.status.id);
                                    if (statusFromDb == null)
                                    {
                                        statusFromDb = (new Status()
                                        {
                                            id = issue.fields.status.id,
                                            name = issue.fields.status.name
                                        });
                                        repository.Create(statusFromDb);
                                    }
                                }
                                //Отправитель
                                Reporter reporterFromDb = null;
                                if (issue.fields.reporter != null)
                                {
                                    reporterFromDb =
                                        repository.Get<Reporter>(
                                            user => user.emailAddress == issue.fields.reporter.emailAddress);
                                    if (reporterFromDb == null)
                                    {
                                        reporterFromDb = (new Reporter()
                                        {
                                            name = issue.fields.reporter.name,
                                            emailAddress = issue.fields.reporter.emailAddress,
                                            displayName = issue.fields.reporter.displayName,
                                        });
                                        repository.Create(reporterFromDb);
                                    }
                                }
                                //Исполнитель
                                Assignee assigneeFromDb = null;
                                if (issue.fields.assignee != null)
                                {
                                    assigneeFromDb =
                                        repository.Get<Assignee>(
                                            user => user.emailAddress == issue.fields.assignee.emailAddress);
                                    if (assigneeFromDb == null)
                                    {
                                        assigneeFromDb = (new Assignee()
                                        {
                                            name = issue.fields.assignee.name,
                                            emailAddress = issue.fields.assignee.emailAddress,
                                            displayName = issue.fields.assignee.displayName,
                                        });
                                        repository.Create(assigneeFromDb);
                                    }
                                }
                                //Приоритет
                                Priority priorityFromDb = null;
                                if (issue.fields.priority != null)
                                {
                                    priorityFromDb =
                                        repository.Get<Priority>(priority => priority.id == issue.fields.priority.id);
                                    if (priorityFromDb == null)
                                    {
                                        priorityFromDb = (new Priority()
                                        {
                                            id = issue.fields.priority.id,
                                            name = issue.fields.priority.name
                                        });
                                        repository.Create(priorityFromDb);
                                    }
                                }
                                //Список меток, связь многие ко многим
                                var ticketLablesList = new List<Lable>();
                                foreach (string lbl in issue.fields.labels)
                                {
                                    var replable = repository.Get<Lable>(lable => lable.value == lbl);
                                    if (replable == null)
                                    {
                                        ticketLablesList.Add(new Lable()
                                        {
                                            value = lbl
                                        });
                                    }
                                    else
                                    {
                                        ticketLablesList.Add(replable);
                                    }
                                }
                            //обрабатываю сам тикет
                            var ticketFromDB = repository.Get<Ticket>(ticket => ticket.Key == issue.key);
                                if (ticketFromDB == null)
                                {
                                    repository.Create(new Ticket()
                                    {
                                        Assignee = assigneeFromDb,
                                        Created = issue.fields.created,
                                        Key = issue.key,
                                        Lable = ticketLablesList,
                                        Priority = priorityFromDb,
                                        Reporter = reporterFromDb,
                                        Status = statusFromDb,
                                        Summary = issue.fields.summary,
                                        TicketType = ticketTypeFromDb,
                                        Updated = issue.fields.updated,  
                                        ResolutionDate = issue.fields.resolutiondate,
                                        AssignTime = assignTime,
                                        TakeInWorkTime = takeInWorkTime,
                                        WaitForAnswerTime = waitForAnswerTime,
                                        SlaAssignIssueFailed = assignIssueFailed,
                                        SlaTakeInWorkFailed = slaTakeInWorkFailed,
                                        SlaWaitForAnswerFailed = slaWaitForAnswer
                                    });
                                }
                                else
                                {
                                    ticketFromDB.Assignee = assigneeFromDb;
                                    ticketFromDB.Created = issue.fields.created;
                                    ticketFromDB.Key = issue.key;
                                    ticketFromDB.Lable = ticketLablesList;
                                    ticketFromDB.Priority = priorityFromDb;
                                    ticketFromDB.Reporter = reporterFromDb;
                                    ticketFromDB.Status = statusFromDb;
                                    ticketFromDB.Summary = issue.fields.summary;
                                    ticketFromDB.TicketType = ticketTypeFromDb;
                                    ticketFromDB.Updated = issue.fields.updated;
                                    ticketFromDB.ResolutionDate = issue.fields.resolutiondate;
                                    ticketFromDB.AssignTime = assignTime;
                                    ticketFromDB.TakeInWorkTime = takeInWorkTime;
                                    ticketFromDB.WaitForAnswerTime = waitForAnswerTime;
                                    ticketFromDB.SlaAssignIssueFailed = assignIssueFailed;
                                    ticketFromDB.SlaTakeInWorkFailed = slaTakeInWorkFailed;
                                    ticketFromDB.SlaWaitForAnswerFailed = slaWaitForAnswer;
                                    repository.Update();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            repository.Create(new Log
                            {
                                Date = DateTime.Now,
                                MessageTipe = "error",
                                Operation = "ProcessTicket",
                                Exception = ex.GetType() + ": " + ex.Message
                            });
                            Thread.Sleep(int.Parse(intervalParam.ValueString));
                        }
                        lastProcessedChangeDateParameter.ValueDate = endCheckChangeDate;
                        repository.Update();
                        Thread.Sleep(int.Parse(intervalParam.ValueString));
                    }
                }
                catch (Exception e)
                {
                    Thread.Sleep(int.Parse(intervalParam.ValueString));
                    repository.Create(new Log
                    {
                        Date = DateTime.Now,
                        MessageTipe = "error",
                        Operation = "CheckJiraError",
                        Exception = e.GetType() + ": " + e.Message
                    });
                }
            }

        }
    }
}