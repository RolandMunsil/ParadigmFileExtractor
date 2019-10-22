# File System Structure

The file system for Paradigm's N64 games is, overall, pretty simple. At a certain point in the ROM, there's a file table which lists the locations of all the files, and then immediately after that are all the files. Most of the games have nothing else in them after the last file, except for some releases of AeroFighters Assault and Pilotwings 64 - I don't know if this extra data is more code or some sort of special file.

Note that there isn't any consistent, easy-to-locate pointer to the file table - it's loaded in different places in each game and in many or all cases is generated directly by the code (as opposed to being copied from somewhere in the ROM). Thankfully, it's easy to find by looking for its header.

One last note before we start - Pilotwings 64 and AeroFighters Assault seem to have some important differences to the other Paradigm games (F-1 World Grand Prix I & II, Beetle Adventure Racing, Duck Dodgers Starring Daffy Duck, and Indy Racing 2000). Most importantly, they have a different file table format, but there are other differences; e.g. they seem to mostly use a different set of file types than the others. Given that they're the company's first two games, and their only flight games, my *guess* is that they switched to a new engine/compiler/workflow/something after AeroFighters Assault. Regardless, I'll refer to those two games as their "Flight Games" and the rest as their "Racing Games" in this document, so I don't have to list them every time. (Yeah, I know Duck Dodgers isn't a racing
game, but "Racing + Duck Dodgers Games" isn't exactly concise :P)

## The File Table
The flight games and the racing games have pretty different file table formats, so I need to explain them separately.

### File Header Note
Before I explain the file table, just a note about how the files themselves are structured - except for some special cases which I'll explain along the way, all entries in the file table start with a four-letter string "FORM", then the length of the file in bytes. The first four bytes of the file are always the file's "magic word", which is just another four-letter string that indicates what type of file is. For example:

TODO: image of first file in BAR

The file's bytes are on the left, and on the right is those bytes interpreted as a string. You can see that the first four bytes are "FORM", and the next four are `0x8BC0`, which means this file will be `0x8BC0` bytes long. Then comes the magic word, which in this case is "MODU".

Also, in the racing games, nearly all file types will start with "UV" - I assume "UV" is the term for their file system, or engine, or something like that, since the "UV" part is also present in the names of the bits of code that actually load the files.

### Racing Game format
The file table itself also has a file header - it starts with "FORM" in ascii, followed by the length of the file table. After that is "UVFT", the file table's magic word. I assume the "FT" stands for File Table. 

After this, there's just a bunch of lists of offsets. These lists start with a four-letter string, which in almost all cases is the magic word of all of the files in the list, and then a bunch of four-byte numbers which are offsets to the starts of files. These offsets are from the first address ending in 0 after the end of the file table. So, for example, if the file table ended at `0x25FC8`, the offsets would be from `0x25FD0`.

TODO: image of file table with explanation

I said "almost all cases" because there are a few special cases. The most notable is the "UVRW" section - the files it refers to never have a "UVRW" magic word, and some of them have no header at all - not even the "FORM"+length part. My guess is that the "RW" in "UVRW" is short for "raw", and this is a general-purpose section for any files that don't fit into the other sections.

Two sections also have a mismatch between the section magic word and the file magic word. The "MODU" section will always have files with the magic word "UVMO", and the "UVSQ" section will have files with the magic word "UVTS" in some games.

Finally, there is one additional hiccup you need to know about. Sometimes entries in the file table will just be `0xFFFFFFFF` instead of file offsets.

TODO: image of FFFFFFFFs

As far as I've seen they don't represent anything important and can be safely skipped over - I've checked and you wont end up skipping any hidden files or anything. I don't really have any solid guesses on why these are there - I have one theory but I need to investigate more before I feel confident in stating it.

### Flight Game format
The flight game file tables are in an almost completely different format. Firstly, the file table is actually compressed. All of the Paradigm games have compressed files, and they have a standard format for them, which the file table follows. I won't explain it here because it's explained in the next section. Instead of being a "UVFT" file, it's a "UVRM" file.

TODO: Image of compressed table

Once you decompress it, you get what is essentially a big list of types followed by sizes.

TODO: Image of decompressed table

The four-letter string is the file type of the file, and the 4-byte number is the size of the file table entry. It's important to empasize that this is the size of the entry, *not the file itself*, so this size includes the 8-byte FORM header. Like the racing game format, the first file starts at the first address ending in 0 after the end of the file table. The pairs  are in the same order as the files themselves in the ROM.

## File Format
I don't understand much about any of the specific file formats at this point, but all of the files use a common structure. (Some other people have figured out some of the formats, though: [here](https://github.com/magcius/pilotwings_64) and [here](https://github.com/magcius/noclip.website/blob/master/src/Pilotwings64/Scenes.ts).

After the magic word, the file consists of a series of sections which follow the format \[section magic word\]\[section data length\]\[section data\], much like the FORM header. For example, take a look at the start of this file:

TODO: image of the start of a file, with some explanation

I don't know what most of these section types mean, but there are a few that I do (and which are important to understand if you want to work on reverse-engineering any of these file formats)

### "GZIP" sections (aka MIO0 sections)
Sections that start with "GZIP" are, confusingly, actually files compressed using the [MIO0 compression format](https://hack64.net/wiki/doku.php?id=super_mario_64:mio0). After the section header is a magic word indicating the type of the data being compressed, followed by the expected size of the data once it's decompressed, and then the actual compressed data (which has it's own header, but that's part of the MIO0 format). I assume that at some point they were using or were planning on using the GZIP compression format at some point, and when they switched to MIO0 they kept the old header.

TODO: labeled image

### "PAD " sections
"PAD " sections always have just 4 bytes of data which is always `0x00000000`. Given the name "PAD", it seems like they're probably there as padding, maybe for alignment?

### "COMM" sections
It seems like "COMM" is a catch-all for any type of data without a defined type - "COMM" seems like it's likely short for "common".

## AeroFighters weirdness
The above explanation covers the filesystems of all of the games Paradigm has released, except for one: AeroFighters Assault (and it's releases in other regions). For some reason these games have some really weird stuff going on in their filesystems.

### Strange magic words
The AeroFighers games have a few files with the magic words "    " (yes, that's just four spaces) and "Trai" (the only magic word with lowercase letters). They all appear together, but I haven't looked into the file format itself to figure out what's special about them, so I have no clue what's up with them.

### Emptiness at the end of files
A few files (around 30) in all of the games have a bunch of 0 bytes after the end of the last section in the file. These 0 bytes are definitely a part of the file, as they are included in both the length in the file table and the length in the header, but again, I have no idea why they're there. In case it helps, they're always UVMD (uvmodel) or UVCT (uvcontour) files.

## That's it!
That's all! Please feel free to email me, message me, or open an issue if anything in this wasn't clear, or if you have any other questions about the file system. The worst thing that could happen is just that I don't respond :)
