using System.Data.Entity;

namespace JiraExtractor
{
    public class DbContext : System.Data.Entity.DbContext
    {
        public DbContext(): base("DbConnection"){ }
        public DbSet<Log> Logs { get; set; }
        public DbSet<Parametr> Parametrs { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<TicketType> TicketType { get; set; }
        public DbSet<Status> Status { get; set; }
        public DbSet<Reporter> Reporter { get; set; }
        public DbSet<Assignee> Assignee { get; set; }
        public DbSet<Priority> Priority { get; set; }
        public DbSet<Lable> Lables { get; set; }
    }
}
