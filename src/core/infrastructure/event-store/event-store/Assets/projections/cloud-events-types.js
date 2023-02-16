fromAll()
    .when({
        $init: function () {
            return {
                types: []
            }
        },
        $any: function (s, e) {
            var type = e.eventType;
            if (type.startsWith('$') || s.types.includes(type)) return;
            s.types.push(type);
        }
    })