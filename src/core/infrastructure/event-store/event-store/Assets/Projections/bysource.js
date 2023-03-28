fromStream('cloud_events')
    .when({
        $any: (_, evt) => {
            linkTo('cloud_events-' + JSON.parse(evt.metadataRaw).contextAttributes.source, evt);
        }
    });