<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
		xmlns:tomboy="http://beatniksoftware.com/tomboy"
		xmlns:size="http://beatniksoftware.com/tomboy/size"
		xmlns:link="http://beatniksoftware.com/tomboy/link"
        xmlns="http://www.w3.org/1999/xhtml"
                version='1.0' exclude-result-prefixes="tomboy size link">

<xsl:output method="html" indent="no" omit-xml-declaration="yes"/>
<xsl:preserve-space elements="*" />

<xsl:param name="newline" select="'&#xA;'" />

<xsl:template match="/">
	<div>
		<xsl:apply-templates select="tomboy:note/tomboy:text/node()"/>
	</div>
</xsl:template>


<xsl:template match="tomboy:note/tomboy:text/*[1]/text()[1]">
	<xsl:value-of select="substring-after(., $newline)"/>
</xsl:template>

<xsl:template match="tomboy:bold">
	<b><xsl:apply-templates select="node()"/></b>
</xsl:template>

<xsl:template match="tomboy:italic">
	<i><xsl:apply-templates select="node()"/></i>
</xsl:template>

<xsl:template match="tomboy:strikethrough">
	<span style="text-decoration: line-through;"><xsl:apply-templates select="node()"/></span>
</xsl:template>

<xsl:template match="tomboy:highlight">
	<span style="background:yellow"><xsl:apply-templates select="node()"/></span>
</xsl:template>

<xsl:template match="tomboy:datetime">
	<span style="font-style:italic;font-size:small;color:grey">
		<xsl:apply-templates select="node()"/>
	</span>
</xsl:template>

<xsl:template match="size:small">
	<span style="font-size:small"><xsl:apply-templates select="node()"/></span>
</xsl:template>

<xsl:template match="size:large">
	<h3><xsl:apply-templates select="node()"/></h3>
</xsl:template>

<xsl:template match="size:huge">
	<h2><xsl:apply-templates select="node()"/></h2>
</xsl:template>

<xsl:template match="link:broken">
	<xsl:value-of select="node()"/>
</xsl:template>

<xsl:template match="link:internal">
	<xsl:value-of select="node()"/>
</xsl:template>

<xsl:template match="link:url">
	<a href="{node()}"><xsl:value-of select="node()"/></a>
</xsl:template>

<xsl:template match="tomboy:list">
	<ul>
		<xsl:apply-templates select="tomboy:list-item" />
	</ul>
</xsl:template>

<xsl:template match="tomboy:list-item">
	<li>
		<xsl:if test="normalize-space(text()) = ''">
			<xsl:attribute name="style">list-style-type: none</xsl:attribute>
		</xsl:if>
		<xsl:attribute name="dir">
			<xsl:value-of select="@dir"/>
		</xsl:attribute>
		<xsl:apply-templates select="node()" />
	</li>
</xsl:template>

<!-- Evolution.dll Plugin -->
<xsl:template match="link:evo-mail">
		<xsl:value-of select="node()"/>
</xsl:template>

<!-- FixedWidth.dll Plugin -->
<xsl:template match="tomboy:monospace">
	<span style="font-family:monospace"><xsl:apply-templates select="node()"/></span>
</xsl:template>

</xsl:stylesheet>

