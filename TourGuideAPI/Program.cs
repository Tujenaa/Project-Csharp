using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Swagger luôn bật (cả Production) để dễ debug
app.UseSwagger();
app.UseSwaggerUI(c =>
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TourGuideAPI v1"));

// Thứ tự middleware RẤT QUAN TRỌNG
app.UseRouting();        // 1. Routing trước
app.UseCors("AllowAll"); // 2. CORS sau Routing
app.UseAuthorization();  // 3. Auth sau CORS
app.UseStaticFiles();

app.MapControllers();
app.Run();