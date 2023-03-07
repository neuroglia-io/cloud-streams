fromStream('cloud-events')
    .when({
        $any: (stream, evt) => {
            linkTo('cloud-events-' + JSON.parse(evt.metadataRaw).source, evt);
        }
    });