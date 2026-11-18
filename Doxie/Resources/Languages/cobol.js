// COBOL for Monaco, which has none. the page loads it after editor.main.js,
// this is the same file as the one of Markup Pad and CodeContour.
// it reads the dialects compilers still accept: the ISO standards from COBOL-85 to COBOL 2023, IBM Enterprise COBOL,
// Micro Focus, ACUCOBOL-GT, GnuCOBOL, Fujitsu NetCOBOL and the object oriented extensions,
// in fixed, variable, free and terminal format, with SQL and CICS embedded.
require(['vs/editor/editor.main'], function () {
    'use strict';

    const id = 'cobol';

    // the same as the COBOL row of CodeContour's language registry, which measures what this colors.
    const extensions = ['.cbl', '.cob', '.cobol', '.cpy', '.ccp', '.pco', '.sqb', '.cblle', '.sqlcblle'];

    monaco.languages.register({ id: id, extensions: extensions, aliases: ['COBOL', 'cobol'] });

    monaco.languages.setLanguageConfiguration(id, {
        comments: { lineComment: '*>' },
        brackets: [['(', ')']],
        autoClosingPairs: [
            { open: '(', close: ')' },
            { open: '"', close: '"', notIn: ['string'] },
            { open: "'", close: "'", notIn: ['string'] },
        ],
        surroundingPairs: [
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: "'", close: "'" },
        ],

        // a COBOL name holds hyphens, WS-TOTAL-AMOUNT is one word to select and to search.
        wordPattern: /[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*/,
    });

    // reserved and context words of every dialect together: a word one compiler reserves is a name in
    // another, but colored as a keyword it still reads right, and a program uses one dialect at a time.
    const keywords = [...new Set([
        // ISO COBOL-85 to COBOL 2023.
        'accept', 'access', 'active-class', 'add', 'address', 'advancing', 'after', 'aligned', 'all', 'allocate',
        'alphabet', 'alphabetic', 'alphabetic-lower', 'alphabetic-upper', 'alphanumeric', 'alphanumeric-edited', 'also',
        'alter', 'alternate', 'and', 'any', 'anycase', 'are', 'area', 'areas', 'arithmetic', 'as', 'ascending', 'assign',
        'at', 'away-from-zero', 'b-and', 'b-not', 'b-or', 'b-shift-l', 'b-shift-lc', 'b-shift-r', 'b-shift-rc', 'b-xor',
        'based', 'before', 'binary', 'binary-char', 'binary-double', 'binary-encoding', 'binary-long', 'binary-short',
        'bit', 'blank', 'block', 'boolean', 'bottom', 'by', 'byte-length', 'call', 'cancel', 'capacity', 'cd', 'cf',
        'ch', 'character', 'characters', 'class', 'class-id', 'clock-units', 'close', 'code', 'code-set', 'col',
        'collating', 'cols', 'column', 'columns', 'comma', 'commit', 'common', 'communication', 'comp', 'computational',
        'compute', 'condition', 'configuration', 'constant', 'contains', 'content', 'continue', 'control', 'controls',
        'converting', 'copy', 'corr', 'corresponding', 'count', 'crt', 'currency', 'cursor', 'data', 'data-pointer',
        'date', 'day', 'day-of-week', 'de', 'debugging', 'decimal-encoding', 'decimal-point', 'declaratives', 'default',
        'delete', 'delimited', 'delimiter', 'depending', 'descending', 'destination', 'detail', 'disable', 'display',
        'divide', 'division', 'down', 'duplicates', 'dynamic', 'ec', 'egi', 'else', 'emi', 'enable', 'end', 'end-accept',
        'end-add', 'end-call', 'end-compute', 'end-delete', 'end-display', 'end-divide', 'end-evaluate', 'end-if',
        'end-multiply', 'end-of-page', 'end-perform', 'end-read', 'end-receive', 'end-return', 'end-rewrite',
        'end-search', 'end-start', 'end-string', 'end-subtract', 'end-unstring', 'end-write', 'entry', 'environment',
        'eo', 'eop', 'equal', 'error', 'esi', 'evaluate', 'every', 'exception', 'exception-object', 'exclusive', 'exit',
        'extend', 'external', 'factory', 'false', 'fd', 'file', 'file-control', 'filler', 'final', 'first',
        'float-binary-128', 'float-binary-32', 'float-binary-64', 'float-decimal-16', 'float-decimal-34',
        'float-extended', 'float-infinity', 'float-long', 'float-not-a-number', 'float-short', 'footing', 'for',
        'format', 'free', 'from', 'function', 'function-id', 'function-pointer', 'generate', 'get', 'giving', 'global',
        'go', 'goback', 'greater', 'group', 'group-usage', 'heading', 'high-value', 'high-values', 'i-o', 'i-o-control',
        'identification', 'if', 'in', 'index', 'indexed', 'indicate', 'inherits', 'initial', 'initialize', 'initiate',
        'input', 'input-output', 'inspect', 'installation', 'interface', 'interface-id', 'intermediate', 'into',
        'intrinsic', 'invalid', 'invoke', 'is', 'just', 'justified', 'key', 'keyboard', 'label', 'last', 'leading',
        'left', 'length', 'less', 'limit', 'limits', 'linage', 'linage-counter', 'line', 'line-counter', 'lines',
        'linkage', 'local-storage', 'locale', 'lock', 'low-value', 'low-values', 'memory', 'merge', 'message', 'method',
        'method-id', 'minus', 'mode', 'move', 'multiple', 'multiply', 'national', 'national-edited', 'native',
        'nearest-away-from-zero', 'nearest-even', 'nearest-toward-zero', 'negative', 'nested', 'next', 'no', 'not',
        'null', 'nulls', 'number', 'numeric', 'numeric-edited', 'object', 'object-computer', 'object-reference', 'occurs',
        'of', 'off', 'omitted', 'on', 'open', 'optional', 'options', 'or', 'order', 'organization', 'other', 'output',
        'overflow', 'override', 'packed-decimal', 'padding', 'page', 'page-counter', 'perform', 'pf', 'ph', 'pic',
        'picture', 'plus', 'pointer', 'position', 'positive', 'prefixed', 'present', 'previous', 'printing',
        'procedure', 'procedure-pointer', 'procedures', 'proceed', 'program', 'program-id', 'program-pointer',
        'prohibited', 'property', 'prototype', 'purge', 'queue', 'quote', 'quotes', 'raise', 'raising', 'random', 'rd',
        'read', 'receive', 'record', 'records', 'redefines', 'reel', 'reference', 'references', 'relative', 'release',
        'remainder', 'removal', 'renames', 'repeated', 'replace', 'replacing', 'report', 'reporting', 'reports',
        'repository', 'reserve', 'reset', 'resume', 'retry', 'return', 'returning', 'rewind', 'rewrite', 'rf', 'rh',
        'right', 'rollback', 'rounded', 'rounding', 'run', 'same', 'screen', 'sd', 'search', 'section', 'secure',
        'security', 'segment', 'segment-limit', 'select', 'self', 'send', 'sentence', 'separate', 'sequence',
        'sequential', 'set', 'sharing', 'short', 'sign', 'signed', 'size', 'sort', 'sort-merge', 'source',
        'source-computer', 'sources', 'space', 'spaces', 'special-names', 'standard', 'standard-1', 'standard-2',
        'standard-binary', 'standard-decimal', 'start', 'status', 'stop', 'string', 'sub-queue-1', 'sub-queue-2',
        'sub-queue-3', 'subtract', 'sum', 'super', 'suppress', 'symbolic', 'sync', 'synchronized', 'system-default',
        'table', 'tallying', 'terminal', 'terminate', 'test', 'text', 'than', 'then', 'through', 'thru', 'time', 'times',
        'to', 'top', 'toward-greater', 'toward-lesser', 'trailing', 'true', 'truncation', 'type', 'typedef', 'unit',
        'universe', 'unlock', 'unsigned', 'unstring', 'until', 'up', 'upon', 'usage', 'use', 'user-default', 'using',
        'val-status', 'valid', 'validate', 'validate-status', 'value', 'values', 'varying', 'when', 'with',
        'working-storage', 'write', 'zero', 'zeroes', 'zeros',

        // IBM Enterprise COBOL, older IBM compilers and their special registers.
        'basis', 'comp-1', 'comp-2', 'comp-3', 'comp-4', 'comp-5', 'computational-1', 'computational-2',
        'computational-3', 'computational-4', 'computational-5', 'current-date', 'dbcs', 'debug-item', 'display-1',
        'egcs', 'eject', 'encoding', 'end-exec', 'end-json', 'end-xml', 'enter', 'examine', 'exec', 'exhibit', 'id',
        'insert', 'jnienvptr', 'json', 'json-code', 'json-status', 'kanji', 'name', 'named', 'parse', 'pointer-32',
        'processing', 'ready', 'remarks', 'return-code', 'service', 'shift-in', 'shift-out', 'skip1', 'skip2', 'skip3',
        'sort-control', 'sort-core-size', 'sort-file-size', 'sort-message', 'sort-mode-size', 'sort-return', 'tally',
        'time-of-day', 'title', 'trace', 'transform', 'unbounded', 'validating', 'when-compiled', 'xml', 'xml-code',
        'xml-event', 'xml-information', 'xml-namespace', 'xml-namespace-prefix', 'xml-nnamespace',
        'xml-nnamespace-prefix', 'xml-ntext', 'xml-schema', 'xml-text',

        // Micro Focus, its object oriented and .NET or JVM extensions, and screen handling.
        'abstract', 'attribute', 'auto', 'auto-skip', 'automatic', 'background-color', 'beep', 'bell', 'blink',
        'catch', 'comp-n', 'comp-x', 'computational-n', 'computational-x', 'convert', 'create', 'crt-under',
        'custom-attribute', 'declare', 'delegate', 'delegate-id', 'end-class', 'end-delegate', 'end-enum',
        'end-factory', 'end-function', 'end-interface', 'end-invoke', 'end-iterator', 'end-method', 'end-object',
        'end-operator', 'end-program', 'end-property', 'end-try', 'end-valuetype', 'enum', 'enum-id', 'eol', 'eos',
        'erase', 'event', 'finally', 'foreground-color', 'full', 'highlight', 'instance', 'internal', 'iterator',
        'iterator-id', 'keep', 'local', 'lowlight', 'manual', 'new', 'no-echo', 'operator', 'operator-id',
        'parameter', 'private', 'prompt', 'protected', 'public', 'redefine', 'required', 'reverse-video', 'sealed',
        'split', 'static', 'tab', 'try', 'underline', 'update', 'valuetype', 'valuetype-id', 'yield', 'zero-fill',

        // ACUCOBOL-GT graphical and thread statements, and its controls.
        'bar', 'bitmap', 'cell', 'check-box', 'color', 'combo-box', 'destroy', 'display-format', 'entry-field',
        'exception-value', 'floating', 'frame', 'graphical', 'grid', 'handle', 'inquire', 'label-offset', 'list-box',
        'menu', 'modify', 'pop-up', 'push-button', 'radio-button', 'scroll-bar', 'status-bar', 'tab-control',
        'thread', 'toolbar', 'tree-view', 'wait', 'web-browser', 'window',

        // GnuCOBOL and Fujitsu NetCOBOL extensions.
        'chain', 'chaining', 'cycle', 'end-chain', 'entry-convention', 'ignore', 'recursive', 'sysin', 'sysout',
        'syserr', 'syslist', 'syslst', 'syspunch', 'console', 'printer', 'argument-number', 'argument-value',
        'environment-name', 'environment-value', 'command-line',
    ])];

    // the headers that give a program its structure, colored apart from the statements under them
    // like Notepad++ does, with the COBOL 2002 and object oriented ones its list lacks, REPOSITORY first.
    const headers = [
        'division', 'section', 'declaratives', 'program-id', 'class-id', 'method-id', 'function-id', 'interface-id',
        'delegate-id', 'enum-id', 'valuetype-id', 'iterator-id', 'operator-id', 'author', 'installation',
        'date-written', 'date-compiled', 'security', 'remarks', 'source-computer', 'object-computer', 'special-names',
        'repository', 'file-control', 'i-o-control', 'end-class', 'end-delegate', 'end-enum', 'end-factory',
        'end-function', 'end-interface', 'end-iterator', 'end-method', 'end-object', 'end-operator', 'end-program',
        'end-property', 'end-valuetype',
    ];

    // the words that start or end a statement, colored apart from the words of its clauses the way Notepad++ does:
    // MOVE and END-IF read in the keyword blue, TO and DELIMITED BY SIZE in a softer one.
    const statements = [
        'accept', 'add', 'allocate', 'also', 'alter', 'call', 'cancel', 'catch', 'chain', 'close', 'commit',
        'compute', 'continue', 'copy', 'declare', 'delete', 'destroy', 'disable', 'display', 'divide', 'else', 'enable',
        'enter', 'entry', 'evaluate', 'examine', 'exec', 'exhibit', 'exit', 'finally', 'free', 'generate', 'go',
        'goback', 'if', 'initialize', 'initiate', 'inquire', 'inspect', 'invoke', 'json', 'merge', 'modify', 'move',
        'multiply', 'open', 'perform', 'purge', 'raise', 'read', 'ready', 'receive', 'release', 'replace', 'reset',
        'resume', 'return', 'rewrite', 'rollback', 'search', 'send', 'set', 'sort', 'start', 'stop', 'string',
        'subtract', 'suppress', 'terminate', 'transform', 'try', 'unlock', 'unstring', 'use', 'validate', 'wait',
        'when', 'write', 'xml', 'yield',
        'end-accept', 'end-add', 'end-call', 'end-chain', 'end-compute', 'end-delete', 'end-display', 'end-divide',
        'end-evaluate', 'end-exec', 'end-if', 'end-invoke', 'end-json', 'end-multiply', 'end-of-page', 'end-perform',
        'end-read', 'end-receive', 'end-return', 'end-rewrite', 'end-search', 'end-start', 'end-string',
        'end-subtract', 'end-try', 'end-unstring', 'end-write', 'end-xml',
    ];

    // the two blues: statements, function names and embedded commands in the keyword one,
    // every other reserved word in the predefined one, the only other blue all of Monaco's themes have.
    const statementToken = 'keyword';
    const wordToken = 'variable.predefined';

    // the words of embedded SQL, Db2, Oracle and the SQL standard, colored inside EXEC SQL.
    const sqlKeywords = [
        'absolute', 'add', 'all', 'allocate', 'alter', 'and', 'any', 'are', 'as', 'asc', 'at', 'avg', 'begin',
        'between', 'by', 'call', 'cascade', 'case', 'cast', 'char', 'character', 'check', 'close', 'coalesce', 'column',
        'commit', 'connect', 'constraint', 'continue', 'count', 'create', 'cross', 'current', 'current_date',
        'current_time', 'current_timestamp', 'cursor', 'date', 'decimal', 'declare', 'default', 'delete', 'desc',
        'describe', 'descriptor', 'distinct', 'double', 'drop', 'else', 'end', 'escape', 'except', 'exec', 'execute',
        'exists', 'fetch', 'first', 'float', 'for', 'foreign', 'found', 'from', 'full', 'function', 'get', 'go', 'goto',
        'grant', 'group', 'having', 'hold', 'immediate', 'in', 'include', 'index', 'indicator', 'inner', 'insert',
        'integer', 'intersect', 'into', 'is', 'isolation', 'join', 'key', 'last', 'left', 'like', 'limit', 'lock',
        'max', 'merge', 'min', 'next', 'not', 'null', 'nullif', 'numeric', 'of', 'offset', 'on', 'only', 'open',
        'optimize', 'or', 'order', 'outer', 'prepare', 'primary', 'prior', 'procedure', 'read', 'references',
        'release', 'revoke', 'right', 'rollback', 'row', 'rows', 'savepoint', 'schema', 'scroll', 'section', 'select',
        'sensitive', 'set', 'smallint', 'sql', 'sqlca', 'sqlcode', 'sqlda', 'sqlerror', 'sqlstate', 'sqlwarning',
        'sum', 'table', 'then', 'timestamp', 'to', 'union', 'unique', 'update', 'uncommitted', 'using', 'values',
        'varchar', 'view', 'when', 'whenever', 'where', 'with', 'work', 'ur', 'cs', 'rs', 'rr',
    ];

    // the commands and options of CICS, colored inside EXEC CICS.
    const cicsKeywords = [
        'abcode', 'abend', 'address', 'asis', 'assign', 'cancel', 'channel', 'commarea', 'container', 'converse',
        'cursor', 'dataonly', 'datapoint', 'dataset', 'delay', 'delete', 'deleteq', 'dequeue', 'dump', 'endbr',
        'enqueue', 'erase', 'eraseaup', 'file', 'flength', 'free', 'freemain', 'from', 'getmain', 'gteq', 'handle',
        'ignore', 'initimg', 'inquire', 'interval', 'into', 'item', 'keylength', 'label', 'length', 'link', 'load',
        'map', 'mapfail', 'mapone', 'maponly', 'mapset', 'nohandle', 'nosuspend', 'notfound', 'numitems', 'program',
        'put', 'queue', 'read', 'readnext', 'readprev', 'readq', 'receive', 'release', 'resetbr', 'resp', 'resp2',
        'retrieve', 'return', 'rewrite', 'ridfld', 'rollback', 'send', 'set', 'start', 'startbr', 'syncpoint', 'sysid',
        'task', 'text', 'time', 'transid', 'ts', 'td', 'unlock', 'update', 'wait', 'write', 'writeq', 'xctl',
    ];

    monaco.languages.setMonarchTokensProvider(id, {
        ignoreCase: true,
        defaultToken: '',
        keywords: keywords,
        headers: headers,
        statements: statements,
        sqlKeywords: sqlKeywords,
        cicsKeywords: cicsKeywords,

        tokenizer: {
            // before any directive, a line that starts like a card is read as fixed format, any other as free.
            // the sequence area is read as digits and blanks only, so a free line is not taken for one by chance.
            root: [
                { include: '@layoutDirectives' },
                [/^([0-9 ]{6})(\*(?:cbl|control)(?![\w-]).*)$/, ['comment', 'keyword.directive']],
                [/^([0-9 ]{6})(\s*(?:cbl|process)\s+[a-z(].*)$/, ['comment', 'keyword.directive']],
                [/^[0-9 ]{6}[*\/].*$/, 'comment'],
                [/^([0-9 ]{6})([dD])(?=\s)/, ['comment', 'keyword']],
                [/^[0-9]{6}(?=\s|$)/, 'comment'],

                // a star or a slash in the first column is a comment in Micro Focus free and ACUCOBOL terminal format.
                [/^[*\/](?!>).*$/, 'comment'],
                { include: '@code' },
            ],

            // fixed or variable format, as a directive said: the six columns before the indicator are never code.
            fixed: [
                { include: '@layoutDirectives' },
                [/^(.{6})(\*(?:cbl|control)(?![\w-]).*)$/, ['comment', 'keyword.directive']],
                [/^(.{6})(\s*(?:cbl|process)\s+[a-z(].*)$/, ['comment', 'keyword.directive']],
                [/^.{6}[*\/].*$/, 'comment'],
                [/^(.{6})([dD])(?=\s)/, ['comment', 'keyword']],
                [/^.{1,6}/, 'comment'],
                { include: '@code' },
            ],

            // free format, as a directive said: no columns at all.
            free: [
                { include: '@layoutDirectives' },
                [/^[*\/](?!>).*$/, 'comment'],
                { include: '@code' },
            ],

            // >>SOURCE FORMAT of the standard, IBM and GnuCOBOL, and SOURCEFORMAT of Micro Focus,
            // switch the layout of the lines after them.
            layoutDirectives: [
                [/>>\s*source(?:\s+format)?(?:\s+is)?\s+free(?![\w-]).*$/, { token: 'keyword.directive', switchTo: '@free' }],
                [/>>\s*source(?:\s+format)?(?:\s+is)?\s+(?:fixed|variable)(?![\w-]).*$/, { token: 'keyword.directive', switchTo: '@fixed' }],
                [/\$\s*set(?![\w-]).*sourceformat\s*[("']*\s*free(?![\w-]).*$/, { token: 'keyword.directive', switchTo: '@free' }],
                [/\$\s*set(?![\w-]).*sourceformat\s*[("']*\s*(?:fixed|variable)(?![\w-]).*$/, { token: 'keyword.directive', switchTo: '@fixed' }],
            ],

            code: [
                [/\*>.*$/, 'comment'],

                // the compiler directives of the standard (>>IF, >>DEFINE, >>D) and of Micro Focus ($SET, $IF),
                // and the options statement IBM puts first in a file.
                [/>>\s*[a-z][\w-]*/, 'keyword.directive'],
                [/\$\s*(?:set|if|else|elif|end|display|region|end-region|define)(?![\w-]).*$/, 'keyword.directive'],
                [/^\s*(?:cbl|process)\s+[a-z(].*$/, 'keyword.directive'],

                // a comment entry of the identification division runs to the end of its line.
                [/(author|installation|date-written|date-compiled|security|remarks)(\s*\.)(.*)$/, ['keyword.flow', 'delimiter', 'comment']],

                // a header is the purple of control keywords in every Monaco theme: a division or a section with
                // its name, a sentence of its own naming the factory or the object, and the end of a program unit.
                [/[a-z0-9-]+\s+(?:division|section)(?![\w-])/, 'keyword.flow'],
                [/(?:factory|object)(?=\s*\.)/, 'keyword.flow'],
                [/end\s+(?:program|class|method|function|interface|factory|object|delegate|enum|valuetype|iterator|operator|property|declaratives)(?![\w-])/, 'keyword.flow'],

                [/exec\s+sql(?![\w-])/, { token: 'keyword', next: '@sql' }],
                [/exec\s+[a-z][\w-]*/, { token: 'keyword', next: '@exec' }],

                // an intrinsic or user function is named right after FUNCTION,
                // and the CICS translator's own functions are written in plain COBOL too.
                [/(function)(\s+)([a-z][\w-]*)/, [wordToken, 'white', statementToken]],
                [/dfh(?:resp|value)(?=\s*\()/, statementToken],

                // the picture string is one token whatever it holds, 9(5)V99 and ZZ,ZZ9.99 alike,
                // and the full stop that ends the sentence is not part of it.
                // every group must match something for Monarch, so the optional IS is a group that can be empty.
                [/(pic(?:ture)?)(\s+)((?:is\s+)?)((?:[^\s.]|\.(?=\S))+)/, [wordToken, 'white', wordToken, 'type']],

                // hexadecimal, national, DBCS, null terminated, boolean and UTF-8 literals have a prefix.
                [/(?:bx?|nx?|nc|u|x|g|z|h)?"/, { token: 'string.quote', next: '@doubleQuoted' }],
                [/(?:bx?|nx?|nc|u|x|g|z|h)?'/, { token: 'string.quote', next: '@singleQuoted' }],

                // the pseudo-text of COPY REPLACING and REPLACE.
                [/==/, 'delimiter.pseudotext'],

                // a word has at least one letter, so 10 stays a number and WS-COUNT-1 stays one word.
                [/[a-z0-9-]*[a-z][a-z0-9-]*/, { cases: { '@headers': 'keyword.flow', '@statements': statementToken, '@keywords': wordToken, '@default': 'identifier' } }],
                [/\d+(?:\.\d+)?(?:e[+-]?\d+)?/, 'number'],

                [/\*\*|>=|<=|<>|[=<>+\-*\/&]/, 'operator'],
                [/[()]/, '@brackets'],
                [/[.,;:]/, 'delimiter'],
                [/\s+/, 'white'],
            ],

            // the sequence area and comment lines of a fixed layout go on inside an embedded block.
            embeddedLines: [
                [/^[0-9 ]{6}[*\/].*$/, 'comment'],
                [/^[0-9]{6}(?=\s|$)/, 'comment'],
                [/\*>.*$/, 'comment'],
            ],

            sql: [
                [/end-exec(?![\w-])/, { token: 'keyword', next: '@pop' }],
                { include: '@embeddedLines' },
                [/--.*$/, 'comment'],

                // a host variable is a COBOL name, hyphens and all.
                [/:[a-z0-9][\w-]*/, 'variable'],
                [/'/, { token: 'string.quote', next: '@singleQuoted' }],
                [/"/, { token: 'string.quote', next: '@doubleQuoted' }],
                [/[a-z_][\w$#@]*/, { cases: { '@sqlKeywords': 'keyword', '@default': 'identifier' } }],
                [/\d+(?:\.\d+)?/, 'number'],
                [/[=<>!+\-*\/|]+/, 'operator'],
                [/[()]/, '@brackets'],
                [/[,.;]/, 'delimiter'],
                [/\s+/, 'white'],
            ],

            // EXEC CICS, DLI and the other preprocessors: their own words, and COBOL names as arguments.
            exec: [
                [/end-exec(?![\w-])/, { token: 'keyword', next: '@pop' }],
                { include: '@embeddedLines' },
                [/'/, { token: 'string.quote', next: '@singleQuoted' }],
                [/"/, { token: 'string.quote', next: '@doubleQuoted' }],
                [/dfh[a-z0-9]*/, statementToken],
                [/[a-z0-9-]*[a-z][a-z0-9-]*/, { cases: { '@cicsKeywords': 'keyword', '@default': 'identifier' } }],
                [/\d+/, 'number'],
                [/[()]/, '@brackets'],
                [/[,.;:]/, 'delimiter'],
                [/\s+/, 'white'],
            ],

            // a quote inside a literal is written twice. a literal left open runs to the end of its line only:
            // the continuation line of fixed format reopens it with a quote of its own.
            doubleQuoted: [
                [/[^"]+$/, { token: 'string', next: '@pop' }],
                [/[^"]+/, 'string'],
                [/""/, 'string.escape'],
                [/"/, { token: 'string.quote', next: '@pop' }],
            ],

            singleQuoted: [
                [/[^']+$/, { token: 'string', next: '@pop' }],
                [/[^']+/, 'string'],
                [/''/, 'string.escape'],
                [/'/, { token: 'string.quote', next: '@pop' }],
            ],
        },
    });
});
