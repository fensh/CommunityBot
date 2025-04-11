using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityBot.Contracts;
using CommunityBot.Handlers.Results;
using CommunityBot.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using IUpdateHandler = CommunityBot.Contracts.IUpdateHandler;

namespace CommunityBot.Services
{
    public class BotService
    {
        private readonly ILogger<BotService> _logger;
        private readonly BotConfigurationOptions _options;
        private readonly ITelegramBotClient _botClient;
        private readonly IEnumerable<IUpdateHandler> _updateHandlers;

        private bool _isStopPolling;

        public BotService(
            ILogger<BotService> logger,
            IOptions<BotConfigurationOptions> options,
            ITelegramBotClient botClient,
            IEnumerable<IUpdateHandler> updateHandlers)
        {
            _logger = logger;
            _options = options.Value;
            _botClient = botClient;
            _updateHandlers = updateHandlers;
        }

        public async Task StartPolling(TimeSpan? timeout = null)
        {
            _isStopPolling = false;
            
            if (timeout.HasValue)
            {
                _botClient.Timeout = timeout.Value;
            }
            var updateReceiver = new QueuedUpdateReceiver(_botClient);
            updateReceiver.StartReceiving();
            
            _logger.LogInformation("Polling started!");
            
            await foreach (var update in updateReceiver.YieldUpdatesAsync())
            {
                await HandleUpdate(update);

                if (_isStopPolling)
                {
                    _logger.LogInformation("Polling stopped!");
                    break;
                }
            }
        }

        public void StopPolling()
        {
            _isStopPolling = true;
        }

        public Task SetWebhook()
        {
            return _botClient.SetWebhook(_options.WebhookUrl);
        }

        public Task DeleteWebhook()
        {
            return _botClient.DeleteWebhook();
        }

        public Task<WebhookInfo> GetWebhookInfo()
        {
            return _botClient.GetWebhookInfo();
        }

        public async Task HandleUpdate(Update update)
        {
            _logger.LogTrace("Received update {update}", update.ToLog());
            
            foreach (var updateHandler in _updateHandlers.OrderByDescending(uh => uh.OrderNumber))
            {
                try
                {
                    var result = await updateHandler.HandleUpdateAsync(update);
                    await SendResult(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error was thrown when trying sending result for handler '{HandlerName}'!", updateHandler.HandlerName);
                    await SendResult(Result.Error(_options.DebugInfoChatIds, updateHandler.HandlerName, ex));
                }
            }
            
            _logger.LogTrace("Handled update {update}", update.ToLog());
        }
        
        private async Task SendResult(IUpdateHandlerResult result)
        {
            switch (result)
            {
                case TextUpdateHandlerResult textResult:
                    await _botClient.SendMessage(
                        textResult.ChatId,
                        textResult.MessageText, 
                        textResult.ParseMode,
                        new ReplyParameters() { MessageId = textResult.ReplyToMessageId },
                        textResult.ReplyMarkup,
                        textResult.DisableWebPagePreview);
                    break;
                
                case PhotoUpdateHandlerResult photoResult:
                    await _botClient.SendPhoto(
                        photoResult.ChatId,
                        photoResult.FileId,
                        photoResult.Caption,
                        photoResult.ParseMode,
                        new ReplyParameters() { MessageId = photoResult.ReplyToMessageId },
                        photoResult.ReplyMarkup);
                    break;
                
                case VideoUpdateHandlerResult videoResult:
                    await _botClient.SendVideo(
                        videoResult.ChatId,
                        videoResult.FileId, 
                        videoResult.Caption,
                        videoResult.ParseMode,
                        new ReplyParameters() { MessageId = videoResult.ReplyToMessageId },
                        videoResult.ReplyMarkup);
                    break;
                
                case MediaGroupUpdateHandlerResult mediaGroupResult:
                    await _botClient.SendMediaGroup(
                        mediaGroupResult.ChatId, 
                        mediaGroupResult.MediaList,
                        new ReplyParameters() { MessageId = mediaGroupResult.ReplyToMessageId });
                    break;
                
                case DocumentUpdateHandlerResult documentResult:
                    await _botClient.SendDocument(
                        documentResult.ChatId,
                        documentResult.File, 
                        caption: documentResult.Caption, 
                        parseMode: documentResult.ParseMode,
                        replyParameters: new ReplyParameters() { MessageId = documentResult.ReplyToMessageId },
                        replyMarkup: documentResult.ReplyMarkup);
                    documentResult.Dispose();
                    break;
                
                case AggregateUpdateHandlerResult aggregateResult:
                    _logger.LogTrace("Start handle aggregate result ({Count})", aggregateResult.InnerResults.Length);
                    foreach (var innerResult in aggregateResult.InnerResults)
                    {
                        await SendResult(innerResult);
                    }
                    _logger.LogTrace("End handle aggregate result");
                    break;
                
                case NothingUpdateHandlerResult:
                    _logger.LogTrace("Doing nothing.'");
                    break;
            }
        }
    }
}
