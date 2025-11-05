using Contracts.Messaging.Configuration;
using Contracts.Messaging.Producers;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using UserService.Application.Services;
using UserService.Messaging.Consumers;
using UserService.Messaging.Producers;
using UserService.Persistence;
using UserService.Profiles;
using UserService.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// EF In-Memory
builder.Services.AddDbContext<UserDbContext>(options => options.UseInMemoryDatabase("UserServiceDb"));

// Kafka config + producer
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<IEventProducer, KafkaEventProducer>();

builder.Services.AddHostedService<OrderCreatedConsumer>();

builder.Services.AddScoped<IUserService, UserService.Application.Services.UserService>();
builder.Services.AddScoped<IUserValidator, UserValidator>();

builder.Services.AddAutoMapper(typeof(UserMappingProfile));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "UserService API", Version = "v1", Description = "Create Users and Retrive Users" });

    // Enable XML comments (auto-generates summaries/descriptions)
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // Optional: group by controller tags
    c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"]! });
    c.DocInclusionPredicate((name, api) => true);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
