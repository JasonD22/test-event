using Events.API.Extensions;
using Events.API.Helpers;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

if (ThreadPool.SetMinThreads(100, 100))
    Console.WriteLine("Setting min threads to 100");

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<DataContext>();

// configure strongly typed settings object & configure DI for application services
builder.Services.Configure<DbSettings>(builder.Configuration.GetSection("DbSettings"));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddApplicationServices();
builder.AddMessageQueue();


var config = builder.Configuration;

int.TryParse(config["OutputCacheInSeconds:Default"], out int defaultCacheinSeconds);
int.TryParse(config["OutputCacheInSeconds:Events"], out int eventsCacheinSeconds); 
int.TryParse(config["OutputCacheInSeconds:Event"], out int eventCacheinSeconds);
int.TryParse(config["OutputCacheInSeconds:Session"], out int sessionCacheinSeconds);

builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder =>
        builder.Expire(TimeSpan.FromSeconds(defaultCacheinSeconds)));

    options.AddPolicy("Events", builder =>
        builder.Expire(TimeSpan.FromSeconds(eventsCacheinSeconds)));

    options.AddPolicy("Event", builder =>
        builder.Expire(TimeSpan.FromSeconds(eventCacheinSeconds))
                .SetVaryByQuery("id"));

    options.AddPolicy("Session", builder =>
        builder.Expire(TimeSpan.FromSeconds(sessionCacheinSeconds))
                .SetVaryByQuery("eventId,SessionId"));

});

ConfigureCors(builder);

var app = builder.Build();

// ensure database and tables exist
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DataContext>();
    await context.Init();
}


// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.UseOutputCache();
app.MapControllers();

app.Run();

static void ConfigureCors(WebApplicationBuilder builder)
{
    // var cors = builder.Configuration.GetValue<string>("Cors:Domains");
    // var domains = cors.Split(";");
    // Log.Logger.Information("configured cors to allow domains: {0}", string.Join(", ", domains));

    builder.Services.AddCors(m =>
    {
        m.AddDefaultPolicy(b =>
        {
            //  b.WithOrigins(domains);
            b.AllowAnyOrigin();
            b.AllowAnyMethod();
            b.AllowAnyHeader();
            //  b.WithExposedHeaders("Content-Disposition");
        });
    });
}

public partial class Program { }