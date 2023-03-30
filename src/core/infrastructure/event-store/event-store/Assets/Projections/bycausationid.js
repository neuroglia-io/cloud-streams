fromStream('cloud-events')
    .when({
        $any: (_, evt) => {
            linkTo('cloud-events-' + JSON.parse(evt.metadataRaw).$causationId, evt);
        }
    });