using ETMP.MessageQueue;
using Events.API.Application.Queries;
using Events.API.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Events.API.Helpers;

namespace Events.API.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var connString = new DataContext(builder.Configuration).GetConnectionString();

            builder.Services.AddDbContext<EventContext>(options =>
            options.UseNpgsql(connString));

            // Configure mediatR
            builder.Services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(Program));

              //  cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
               // cfg.AddOpenBehavior(typeof(ValidatorBehavior<,>));
              //  cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });


            builder.Services.AddTransient<IEventQueries, EventQueries>();
        }

        public static void AddMessageQueue(this IHostApplicationBuilder builder)
        {
            var messageQueueSettings = new MessageQueueSettings();
            builder.Configuration.GetSection("MessageQueue").Bind(messageQueueSettings);
            
            builder.Services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(messageQueueSettings.Host, messageQueueSettings.Vhost, h =>
                    {
                        h.Username(messageQueueSettings.Username);
                        h.Password(messageQueueSettings.Password);
                    });

                    var timeoutInSeconds = builder.Configuration.GetSection("MassTransit").GetValue<int>("TimeoutInSeconds", MassTransitDefaults.TIMEOUT_IN_SECONDS);

                    cfg.UseTimeout(timeoutConfigurator =>
                    {
                        timeoutConfigurator.Timeout = TimeSpan.FromSeconds(timeoutInSeconds);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            });
        }
    }
}