using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder => builder.AllowAnyOrigin()
                                                           .AllowAnyHeader()
                                                           .AllowAnyMethod()
                                                           .WithExposedHeaders("Content-Disposition")
                                                           .Build());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseCors("AllowAllOrigins");

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// Верхняя регистрация маршрутов
app.MapGet("/", async context =>
{
    await context.Response.WriteAsync("Welcome to Document Formatting Web API...", Encoding.UTF8);
});

app.MapControllers();

app.Run();