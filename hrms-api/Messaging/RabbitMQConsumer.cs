//using Hrms.Api.Messaging.Message_Handlers;
//using Microsoft.Extensions.Options;
//using RabbitMQ.Client;
//using RabbitMQ.Client.Events;
//using System.Text;

//namespace Hrms.Api.Messaging;

//public class RabbitMQConsumer : BackgroundService
//{
//    private readonly RabbitMQSettings _settings;
//    private readonly IServiceScopeFactory _scopeFactory;

//    public RabbitMQConsumer(
//        IOptions<RabbitMQSettings> options,
//        IServiceScopeFactory scopeFactory)
//    {
//        _settings = options.Value;
//        _scopeFactory = scopeFactory;
//    }

//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        try
//        {
//            var factory = new ConnectionFactory
//            {
//                HostName = _settings.Host,
//                Port = _settings.Port,
//                UserName = _settings.Username,
//                Password = _settings.Password,
//                AutomaticRecoveryEnabled = true
//            };

//            var connection = await factory.CreateConnectionAsync();
//            var channel = await connection.CreateChannelAsync();

//            await channel.QueueDeclareAsync(
//                queue: _settings.HrmsQue,
//                durable: true,
//                exclusive: false,
//                autoDelete: false
//            );

//            var consumer = new AsyncEventingBasicConsumer(channel);

//            consumer.ReceivedAsync += async (sender, ea) =>
//            {
//                var body = ea.Body.ToArray();
//                var message = Encoding.UTF8.GetString(body);
//                var routingKey = ea.RoutingKey;
//                var deliveryTag = ea.DeliveryTag;

//                using var scope = _scopeFactory.CreateScope();
//                var handlerFactory = scope.ServiceProvider.GetRequiredService<IMessageHandlerFactory>();

//                try
//                {
//                    var handler = handlerFactory.Create(routingKey);
//                    await handler.Handle(message);

//                    await channel.BasicAckAsync(deliveryTag, multiple: false);
//                    //Console.WriteLine($"[✓] Message processed: {routingKey} | {deliveryTag}");
//                }
//                catch (Exception ex)
//                {
//                    //Console.WriteLine($"[!] Error processing message: {routingKey} | {ex.Message}");
//                    await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
//                }
//            };

//            await channel.BasicConsumeAsync(
//                queue: _settings.HrmsQue,
//                autoAck: false,
//                consumer: consumer
//            );

//            while (!stoppingToken.IsCancellationRequested)
//            {
//                await Task.Delay(1000, stoppingToken);
//            }

//            await channel.CloseAsync();
//            await connection.CloseAsync();
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"[✓] Message processed: {ex.Message}");
//        }
//    }
//}