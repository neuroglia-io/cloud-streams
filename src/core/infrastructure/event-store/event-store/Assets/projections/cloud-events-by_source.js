fromStream('cloud-events')
    .when({
        $any: function (stream, e) {
            linkTo('cloud-events-' + JSON.parse(e.metadataRaw).source, e);
        }
    })