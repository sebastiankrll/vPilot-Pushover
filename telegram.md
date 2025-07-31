# Creating Telegram Bot

1. Message BotFather (@BotFather) in Telegram with `/newbot`
2. Choose a display name by replying. We recommend using `vPilot`.
3. Choose an unique username by replying. The username must end with `_bot`.
4. This will generate a reply with a BotToken and link to join the chat with your bot. Join the chat.
5. Send a message to your bot, e.g., `Hello world`.
6. Open a browser and navigate to `https://api.telegram.org/bot<API-token>/getUpdates`. Replace `<API-token>` with the token obtainend in step 4.
7. In the browser you will see your message object from which you can copy the CHAT_ID. An example of how such a message looks like is shown below: 
```
{
    "update_id": <...>,
    "message": {
        "message_id": <...>,
        "from": {
            "id": <...>,
            "first_name": "<...>"
        },
        "chat": {
            "id": <CHAT_ID>,
            "title": "<GROUP_NAME>"
        },
        "date": <...>,
        "new_chat_participant": {
            "id": <...>, 
            "first_name": "<NAME>",
            "username": "<YOUR_BOT_NAME>"
        }
    }
```

# Configure Telegram Bot (optional, but recommended)

1. Message BotFather (@BotFather) with `/help` to see available commands.
2. Send `/mybots` to see the list of your active bots and select your vPilot bot.
4. Select `Edit Bot`.
5. Select `Edit Botpic` and send a profile picture for your bot. Here is a recommendation:
https://vatsim-forums.nyc3.digitaloceanspaces.com/monthly_2020_08/Vatsim-social_icon.thumb.png.e9bdf49928c9bd5327f08245a68d8304.png
6. After successfully updating the Botpic select `Back to Bot` and `Edit Bot` again.
7. Select `Edit Commands` and paste the commands below.
8. Great, your bot is now fully configured! You can now either use the command menu or start typing `/` in your bot's chat to quickly access commands.
```
conn - Connect to network
disc - Disconnect from network
chat - Open a chat
cancel - Cancel or close chat
radio - Toggle radio listening
help - Show available commands
```

# How to use commands

Send one of the available commands to trigger the described action in vPilot. Initially, you simply send the command, after which the server will respond with a request for the ✏️ command parameters. For example, sending `/conn` will prompt you to enter the connection parameters in the format `<callsign>:<typecode>:[<selcalcode>]`, e.g. `EWG2PA:A319`. The brackets around `[<selcalcode>]` indicate an optional parameter.

Using the `/radio` command, you can forward all broadcast radio messages on the current frequency, not just those directed at you. To do this, you must enable the `[RelayRadio]` option in the `vPilot-Pushover.ini` settings file.

Whether you receive a ✉️ private text message or a 📻 radio message, you can respond using the `/chat` command. You can open either the radio chat for the current frequency or a private chat with a specific callsign. Once you have opened a 💬 chat, all non-command text messages without the leading `/` will be sent to the selected chat as long as you do not change or close the chat with `/cancel`.