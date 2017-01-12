<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    <xsl:output method="xml" indent="yes"/>
    <xsl:template match="/testResults">
        <xsl:element name="testsuite">
            <xsl:attribute name="name">
                <xsl:text>jmeter</xsl:text>
            </xsl:attribute>
            <xsl:attribute name="tests">
                <xsl:value-of select="count(//httpSample)"/>
            </xsl:attribute>
            <xsl:attribute name="errors">
                <xsl:value-of select="count(//httpSample/assertionResult[error='true'])"/>
            </xsl:attribute>
            <xsl:attribute name="failures">
                <xsl:value-of select="count(//httpSample/assertionResult[failure='true'])"/>
            </xsl:attribute>

            <xsl:for-each select="//httpSample">
                <xsl:element name="testcase">
                    <xsl:attribute name="classname">
                        <xsl:value-of select="@lb"/>
                    </xsl:attribute>
                    <xsl:attribute name="name">
                        <xsl:value-of select="./java.net.URL"/>
                    </xsl:attribute>
                    <xsl:attribute name="time">
                        <xsl:value-of select="@t"/>
                    </xsl:attribute>
                    <xsl:if test="./assertionResult/failure='true'">
                        <xsl:element name="failure">
                            <xsl:attribute name="message">
                                <xsl:value-of select="./assertionResult/failureMessage"/>
                            </xsl:attribute>
                        </xsl:element>
                    </xsl:if>
                    <xsl:if test="./assertionResult/error='true'">
                        <xsl:element name="error">
                            <xsl:attribute name="message">
                                <xsl:value-of select="./assertionResult/errorMessage"/>
                            </xsl:attribute>
                        </xsl:element>
                    </xsl:if>
                </xsl:element>
            </xsl:for-each>
        </xsl:element>
    </xsl:template>
</xsl:stylesheet>