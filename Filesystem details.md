# File System Structure

(Note: whenever I refer to a game, I'm always referring to all of its releases unless I specifically say otherwise.)

The file system for Paradigm's N64 games is, overall, pretty simple. At a certain point in the ROM, there's a file table which lists the locations of all the files, and then immediately after that are all the files. Most of the games have nothing else in them after the last file, except for Pilotwings 64 - I don't know if this extra data is more code or some sort of special file.

Note that there isn't any consistent, easy-to-locate pointer to the file table - it's loaded in different places in each game and in many or all cases is generated directly by the code (as opposed to being copied from somewhere in the ROM). Thankfully, it's easy to find by looking for its header.

One last note before we start - Pilotwings 64 and AeroFighters Assault seem to have some important differences to the other Paradigm games (F-1 World Grand Prix I & II, Beetle Adventure Racing, Duck Dodgers Starring Daffy Duck, and Indy Racing 2000). Most importantly, they have a different file table format, but there are other differences; e.g. they seem to mostly use a different set of file types than the others. Given that they're the company's first two games, and their only flight games, my *guess* is that they switched to a new engine/compiler/workflow/something after AeroFighters Assault. Regardless, I'll refer to those two games as their "flight games" and the rest as their "racing games" in this document, so I don't have to list them every time. (Yeah, I know Duck Dodgers isn't a racing
game, but "Racing + Duck Dodgers Games" isn't exactly concise :P)

## The File Table
The flight games and the racing games have pretty different file table formats, so I need to explain them separately.

### Before we get into it
Before I explain the file table, just a note about how the files themselves are structured - except for some special cases which I'll explain along the way, all entries in the file table start with a four-letter string "FORM", then the length of the file in bytes. The first four bytes of the file are always the file's "magic word", which is just another four-letter string that indicates what type of file it is. For example:

![](https://github.com/RolandMunsil/ParadigmFileExtractor/blob/master/Documentation%20Images/file-header.png)

The file's bytes are on the left (with a space after every fourth byte), and on the right is those bytes interpreted as a string. You can see that the first four bytes are "FORM", and the next four are `0x8BC0`, which means this file will be `0x8BC0` bytes long. Then comes the magic word, which in this case is "MODU", so this file is a `MODU` file.

Also, in the racing games, nearly all file types will start with "UV" - almost certainly because their in-house 3D development tool was called "Vega UltraVision".

### Racing game format
The file table itself also has a file header - it starts with the string "FORM", followed by the length of the file table. After that is "UVFT", the file table's magic word. I assume the "FT" stands for File Table. 

After this, there's just a bunch of lists of offsets. These lists start with a four-letter string, which in almost all cases is the magic word of all of the files in the list, then the length of the list in bytes, and then a bunch of four-byte numbers which are offsets to the starts of files. These offsets are from the first address ending in 0 after the end of the file table. So, for example, if the file table ended at `0x25FC8`, the offsets would be from `0x25FD0`.

![](https://github.com/RolandMunsil/ParadigmFileExtractor/blob/master/Documentation%20Images/file-table.png)

In this example, we have a UVPX file list that's `0xC` bytes long, followed by file offsets. Then a UVMO list that's `0x188` bytes long, followed by a bunch of file offsets.

I said "almost all cases" because there are a few special cases. The most notable is the "UVRW" section - the files it refers to never have a "UVRW" magic word, and some of them have no header at all - not even the "FORM"+length part. My guess is that the "RW" in "UVRW" is short for "raw", and this is a general-purpose section for any files that don't fit into the other sections.

Two sections also have a mismatch between the section magic word and the file magic word. The "MODU" section will always have files with the magic word "UVMO", and the "UVSQ" section will have files with the magic word "UVTS" in some games.

Finally, there is one additional hiccup you need to know about. Sometimes entries in the file table will just be `0xFFFFFFFF` instead of file offsets.

![](https://github.com/RolandMunsil/ParadigmFileExtractor/blob/master/Documentation%20Images/ffffffffs-in-file-table.png)

These are only important if you're locating files by index into the table. When the game loads a file, it does so using a file type and index - so e.g. `LoadFile("UVTX", 10)` would load the file in the UVTX section at index 10, and those `0xFFFFFFFF` entries don't get skipped over or anything. I assume that these were files that existed at some point but were removed, and updating all the files that reference them would have been a pain? Or something along those lines. Regardless, they are just dummy values and do not represent data stored elsewhere - all you need to know is don't skip them if you're working with indices into the file table.

### Flight game format
The flight game file tables are in an almost completely different format. Firstly, the file table is actually compressed. All of the Paradigm games have compressed files, and they have a standard format for them, which the file table follows. I won't explain it here because it's explained in the next section. Instead of being a "UVFT" file, it's a "UVRM" file.

![](https://github.com/RolandMunsil/ParadigmFileExtractor/blob/master/Documentation%20Images/compressed-file-table.png)

Once you decompress it, you get what is essentially a big list of types followed by sizes:

![](https://github.com/RolandMunsil/ParadigmFileExtractor/blob/master/Documentation%20Images/decompressed-file-table.png)

The four-letter string is the file type of the file, and the 4-byte number is the size of the file table entry. It's important to empasize that this is the size of the entry, *not the file itself*, so this size includes the 8-byte FORM header. Like the racing game format, the first file starts at the first address ending in 0 after the end of the file table. The pairs  are in the same order as the files themselves in the ROM.

## File Format
I don't understand much about any of the specific file formats at this point, but all of the files use a common structure. (Some other people have figured out some of the formats, though: see [here](https://github.com/magcius/pilotwings_64) and [here](https://github.com/magcius/noclip.website/blob/master/src/Pilotwings64/Scenes.ts)).

After the magic word, the file consists of a series of sections which follow the format \[section magic word\]\[section data length\]\[section data\], much like the FORM header. For example, take a look at the start of this file:

![](https://github.com/RolandMunsil/ParadigmFileExtractor/blob/master/Documentation%20Images/file-sections.png)

This file starts off with a PAD section that's 4 bytes long and just contains `0x00000000`, and then another PAD section with the same data. Then we have a COMM section that's `0x18` bytes long with some data, and then some `PART` sections which are each `0x80` bytes long.

I don't know what most of these section types mean, but there are a few that I do (and which are important to understand if you want to work on reverse-engineering any of these file formats).

### "GZIP" sections (aka MIO0 sections)
Sections that start with "GZIP" are, confusingly, actually files compressed using the [MIO0 compression format](https://hack64.net/wiki/doku.php?id=super_mario_64:mio0). After the section header is a magic word indicating the type of the data being compressed, followed by the expected size of the data once it's decompressed, and then the actual compressed data (which has its own header, but that's part of the MIO0 format). I assume that at some point they were using or were planning on using the GZIP compression format, and when they switched to MIO0 they kept the old header.

![](https://github.com/RolandMunsil/ParadigmFileExtractor/blob/master/Documentation%20Images/gzip-section.png)

In this example, the GZIP section itself is `0xA0` bytes long. It contains data in the `BITM` format, and when decompressed, will be `0x1A0` bytes long. Then what follows is the MIO0 data itself.

### "PAD " sections
"PAD " sections always have just 4 bytes of data which is always `0x00000000`. Given the name "PAD", and the fact that they always consist of just zeroes, it seems like they're probably there as padding, maybe for alignment?

### "COMM" sections
It seems like "COMM" is a catch-all for any type of data without a defined type - "COMM" might be short for "common".

## AeroFighters weirdness
The above explanation covers the filesystems of all of the games Paradigm has released, except for one: AeroFighters Assault (and its releases in other regions). For some reason these games have some really weird stuff going on in their filesystems.

### Strange magic words
The AeroFighers games have a few files with a magic word that's just four spaces, and a few with the magic word "Trai" (the only magic word with lowercase letters). These files are all together in the ROM, but I haven't looked into the file format itself to figure out what's special about them, so I have no clue what's up with them.

### Emptiness at the end of files
A few files (around 30) have a bunch of 0 bytes after the end of the last section in the file. These 0 bytes are definitely a part of the file, as they are included in both the length in the file table and the length in the header, but again, I have no idea why they're there. They're only in some UVMD (uvmodel) and UVCT (uvcontour) files.

## That's it!
That's all! Please feel free to email me, message me, or open an issue if anything in this wasn't clear, or if you have any other questions about the file system. The worst thing that could happen is just that I don't respond :)
