using Xipha.Crawl.NationalFormulary.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// IMPORTANT: point this at the SAME sqlite file the crawler console app writes to,
// so the registry reflects what the crawler has collected.
var dbPath = builder.Configuration.GetConnectionString("Sqlite")
    ?? Path.Combine(AppContext.BaseDirectory, "xipha.sqlite");
builder.Services.AddSingleton<IStorage>(new SqliteStorage(dbPath));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<IStorage>().InitializeAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();