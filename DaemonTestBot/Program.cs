using DaemonTestBot;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<BotSettings>(builder.Configuration);
builder.Services.AddHostedService<TelegramBot>();
var app = builder.Build();

app.Run();
