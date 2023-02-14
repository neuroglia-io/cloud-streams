fromStream('cse')
    .when({
        $any: function (stream, e) {
            linkTo('cse-by_source-' + JSON.parse(e.metadataRaw).source, e);
        }
    })