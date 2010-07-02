CC = gmcs2

NAME = Blogposter
OUT = $(NAME).dll

all:
	$(CC) -debug -out:$(OUT) -target:library -pkg:tomboy-addins \
	-r:System.Web -r:Mono.Posix AuthenticationTypes.cs -pkg:gtk-sharp-2.0 \
	Blogposter.cs BlogposterPreferences.cs Misc.cs \
	-resource:$(NAME).xsl \
	-resource:$(NAME).addin.xml

install:
	cp $(OUT) ~/.tomboy/addins/

clean:
	rm -f *.dll *.mdb
