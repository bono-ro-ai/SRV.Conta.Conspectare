using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class JobExecutionMap : ClassMap<JobExecution>
{
    public JobExecutionMap()
    {
        Table("audit_job_executions");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.JobName).Column("job_name").Not.Nullable();
        Map(x => x.InstanceId).Column("instance_id").Not.Nullable();
        Map(x => x.StartedAt).Column("started_at").Not.Nullable();
        Map(x => x.CompletedAt).Column("completed_at");
        Map(x => x.DurationMs).Column("duration_ms");
        Map(x => x.Status).Column("status").Not.Nullable();
        Map(x => x.ItemsProcessed).Column("items_processed");
        Map(x => x.ErrorMessage).Column("error_message").Length(2000);
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
    }
}
