# Translation
At the moment messages are translated one by one:
1. The client requests a message to be translated by providing the channel and message Ids aswell as the target language
1. The server acquires the message
1. The server runs the messages body through the translation service (Which merely sends an HTTP request to Azures Translator server)
1. The server resolves the clients request with the message object but the body replaced with the translation

Ideally translations would be saved alongside the originals, and users given some more options on the handling of languages:
* A list of languages that the user is comfortable with
* How messages detected as an "uncomfortable" language should be handled: Auto translate or translate on user request.

This way costly use of third party translation services is minimized, and users can tailor their experience in a way which minimizes friction for their use.