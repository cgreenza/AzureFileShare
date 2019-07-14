var config = require('./config.json');

const express = require('express')
var fs = require('fs')
var https = require('https')

const app = express();

app.use(express.static('public'));
const proxy = require('express-http-proxy');
app.use('/', proxy(config.proxyDestinationUrl, {
    preserveHostHdr: true
}))

// gnerate self-signed cert using:
// openssl req -nodes -new -x509 -keyout server.key -out server.cert

https.createServer({
    key: fs.readFileSync('server.key'),
    cert: fs.readFileSync('server.cert')
}, app).listen(443, () => {
    console.log('Dev server listening on port 443, browse to ' + config.localUrl)
});
