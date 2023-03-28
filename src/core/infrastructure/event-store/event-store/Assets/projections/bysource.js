fromStream('cloud_events')
    .when({
        $any: (stream, evt) => {
            linkTo('cloud_events-' + JSON.parse(evt.metadataRaw).contextAttributes.source, evt);
        }
    });