var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rmq")
    .WithManagementPlugin();

builder.Build().Run();
