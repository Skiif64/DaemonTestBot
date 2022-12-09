using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DaemonTestBot
{
    public class TelegramBot : IHostedService
    {
        private readonly ITelegramBotClient _client;
        private readonly ILogger<TelegramBot> _logger;
        private readonly BotSettings _settings;
        private static int __ticks = 0;
        private bool _running;
        public TelegramBot(IOptions<BotSettings> settings, ILogger<TelegramBot> logger)
        {
            _settings = settings.Value;
            _client = new TelegramBotClient(_settings.BotToken);
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var recieverOptions = new ReceiverOptions
            {
                AllowedUpdates = { },
                ThrowPendingUpdates = true
            };
            _client.StartReceiving(
                updateHandler: HandleUpdate,
                receiverOptions: recieverOptions,
                pollingErrorHandler: HandleError,
                cancellationToken: cancellationToken);
            var me = await _client.GetMeAsync(cancellationToken);
            _logger.LogInformation($"Bot @{me.Username} started listening.");

            await StartTestSchedule(cancellationToken);
        }

        private async Task StartTestSchedule(CancellationToken cancellationToken)
        {
            _running = true;
            var interval = TimeSpan.FromMinutes(10);
            var text = $"Прошло 10 минут. Бот все еще работает. Текущее количество тиков: {__ticks}";
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval);
                __ticks++;
                await _client.SendTextMessageAsync(
                    chatId: _settings.ChatId,
                    text: text,
                    cancellationToken: cancellationToken
                    );
            }
        }

        private async Task HandleUpdate(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
        {
            if (update.Type != UpdateType.Message)
                return;

            var message = update.Message!;
            var messageText = message.Text!;

            var handler = messageText switch
            {
                "/start" => OnStartMessageRecieved(_settings.ChatId, cancellationToken),
                "Статус" => OnStatusMessageRecieved(_settings.ChatId, cancellationToken),
                _ => OnStatusMessageRecieved(_settings.ChatId, cancellationToken)
            };
            await handler;
        }

        private async Task OnStartMessageRecieved(long chatId, CancellationToken cancellationToken)
        {
            var text = "Привет! Это бот для проеверки systemd и того, как он будет отрабатывать длительное время";
            var keyboardMarkup = new ReplyKeyboardMarkup(
                new KeyboardButton("Статус"));
            await _client.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                replyMarkup: keyboardMarkup,
                cancellationToken: cancellationToken
                );
            if (!_running)
                await StartTestSchedule(cancellationToken);
        }

        private async Task OnStatusMessageRecieved(long chatId, CancellationToken cancellationToken)
        {
            string text;
            if (_running)
                text = $"Работает. Текущее количество тиков: {__ticks}";
            else
                text = "Не работает.";
            await _client.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                cancellationToken: cancellationToken);
        }

        private async Task HandleError(ITelegramBotClient client, Exception exception, CancellationToken cancellationToken)
        {
            var message = exception switch
            {
                ApiRequestException requestException => $"Ошибка API Telegram. Статус: {requestException.ErrorCode}\n " +
                $"{requestException.Message}",
                _ => exception.ToString()
            };
            _logger.LogCritical(message);
            await _client.SendTextMessageAsync(
                chatId: _settings.ChatId,
                text: message,
                cancellationToken: cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _running = false;
            var text = "Бот завершает работу.";
            await _client.SendTextMessageAsync(
                chatId: _settings.ChatId,
                text: text,
                cancellationToken: cancellationToken);
            
        }
    }
}
