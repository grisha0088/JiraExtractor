using System;
using System.Collections.Generic;



namespace JiraExtractor
{
    public class Ticket
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Summary { get; set; }
        public List<Lable> Lable { get; set; }
        public TicketType TicketType { get; set; }
        public Status Status { get; set; }
        public Priority Priority { get; set; }
        public Reporter Reporter { get; set; }
        public Assignee Assignee { get; set; }
        public long? AssignTime { get; set; }
        public long? TakeInWorkTime { get; set; }
        public long? WaitForAnswerTime { get; set; }
        public bool? SlaAssignIssueFailed { get; set; }
        public bool? SlaTakeInWorkFailed { get; set; }
        public bool? SlaWaitForAnswerFailed { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Updated { get; set; }
        public DateTime? ResolutionDate { get; set; }
    }
}


