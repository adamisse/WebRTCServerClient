using Signaler;
using Signaler.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddSingleton<Mixer>();
builder.Services.AddSingleton<IRoomManager, RoomManager>();
builder.Services.AddSingleton<IUserManager, UserManager>();
builder.Services.AddSingleton<IPeerConnectionManager, PeerConnectionManager>();


builder.Services.AddSignalR(o =>
{
    o.EnableDetailedErrors = true;
});

builder.Services.AddCors(c =>
{
    c.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins("https://websocketking.com/", "https://localhost:4200");
        builder.AllowAnyMethod();
        builder.AllowAnyHeader();
        builder.AllowCredentials();
        builder.SetIsOriginAllowed((host) => true);
    });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<WebRTCHub>("/rtc");
    endpoints.MapControllers();
});

app.MapControllers();

app.Run();
