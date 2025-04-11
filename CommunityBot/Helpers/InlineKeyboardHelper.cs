using CommunityBot.Contracts;
using Telegram.Bot.Types.ReplyMarkups;

namespace CommunityBot.Helpers
{
    public static class InlineKeyboardHelper
    {
        public static InlineKeyboardMarkup GetPostButtons()
        {
            return new InlineKeyboardMarkup(new []
            {
                InlineKeyboardButton.WithCallbackData("❌ Спам!", "report"), 
                InlineKeyboardButton.WithCallbackData("🧡", "like")
            });
        }

        public static ReplyMarkup? GetWelcomeButton(WelcomeMessage welcomeMessage)
        {
            if (welcomeMessage.ButtonLink.IsBlank() || welcomeMessage.ButtonName.IsBlank())
            {
                return null;
            }
            
            return new InlineKeyboardMarkup(
                InlineKeyboardButton.WithUrl(
                    welcomeMessage.ButtonName,
                    welcomeMessage.ButtonLink));
        }
    }
}
