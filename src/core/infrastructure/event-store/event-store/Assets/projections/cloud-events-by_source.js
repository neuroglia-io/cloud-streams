fromStream('cloud-events')
    .when({
        $any: function (stream, evt) {
            linkTo('cloud-events-' + JSON.parse(evt.metadataRaw).source, evt);
        }
    })