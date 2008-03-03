NAME = Blogposter
OUT = $(NAME).dll

all:
	gmcs -debug -out:$(OUT) -target:library -pkg:tomboy-addins \
	-r:System.Web -r:Mono.Posix AuthenticationTypes.cs Blogposter.cs \
	BlogposterPreferences.cs Misc.cs \
	-resource:$(NAME).xsl \
	-resource:$(NAME).addin.xml

install:
	cp $(OUT) ~/.tomboy/addins/

clean:
	rm *.dll *.mdb
